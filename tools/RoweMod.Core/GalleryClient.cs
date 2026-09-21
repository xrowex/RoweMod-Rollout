using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RoweMod.Core;

public sealed class GalleryClient
{
    public const string DefaultOwner = "xrowex";
    public const string DefaultName = "RoweMod-Gallery";
    public const string DefaultRepo = DefaultOwner + "/" + DefaultName;
    public const string RawRoot = "https://raw.githubusercontent.com/" + DefaultRepo + "/main/";

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    readonly string? _token;

    public GalleryClient(string? token = null) => _token = token;

    public static HttpClient Http(string? token = null)
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RoweMod");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        if (!string.IsNullOrWhiteSpace(token))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    public async Task<CatalogFile> FetchCatalogAsync(CancellationToken cancel = default)
    {
        using var http = Http();
        var url = RawRoot + "catalog.json";
        try
        {
            var json = await http.GetStringAsync(url, cancel);
            return JsonSerializer.Deserialize<CatalogFile>(json, Json) ?? new CatalogFile();
        }
        catch (HttpRequestException)
        {
            return new CatalogFile();
        }
    }

    public async Task DownloadModAsync(string repo, CatalogEntry entry, Action<string>? log = null, CancellationToken cancel = default)
    {
        var dest = AppPaths.WorkshopMod(repo, entry.Id);
        if (Directory.Exists(dest))
            Directory.Delete(dest, recursive: true);
        Directory.CreateDirectory(dest);

        using var http = Http(_token);
        var files = await ListModFilesAsync(http, entry, cancel);
        if (files.Count == 0)
            files.AddRange(GuessFiles(entry));

        foreach (var rel in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var url = RawRoot + entry.Path.TrimEnd('/') + "/" + rel.Replace('\\', '/');
            var destFile = Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            try
            {
                var bytes = await http.GetByteArrayAsync(url, cancel);
                await File.WriteAllBytesAsync(destFile, bytes, cancel);
                log?.Invoke("got " + rel);
            }
            catch (HttpRequestException ex)
            {
                log?.Invoke("skip " + rel + " (" + ex.Message + ")");
            }
        }
        if (!File.Exists(Path.Combine(dest, "item.json")))
            throw new InvalidOperationException("Download failed for " + entry.Id + " (no item.json).");
    }

    public async Task SyncSubscriptionsAsync(string repo, Action<string>? log = null, CancellationToken cancel = default)
    {
        var catalog = await FetchCatalogAsync(cancel);
        var subs = SubscriptionStore.Load();
        foreach (var id in subs.Ids.ToArray())
        {
            var entry = catalog.Mods.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                log?.Invoke("not on gallery: " + id);
                continue;
            }
            log?.Invoke("sync " + id);
            await DownloadModAsync(repo, entry, log, cancel);
        }
    }

    public async Task<string> SubmitPrAsync(string packageDir, string title, Action<string>? log = null, CancellationToken cancel = default)
    {
        var token = _token ?? throw new InvalidOperationException("GitHub login required to submit.");
        using var http = Http(token);
        var user = await GetLogin(http, cancel);
        log?.Invoke("GitHub user " + user);

        var info = JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(Path.Combine(packageDir, "mod.json")), Json)
            ?? throw new InvalidOperationException("mod.json missing");
        var branch = "submit/" + info.Id + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var headRepo = DefaultRepo;
        var canPush = await CanPush(http, DefaultOwner, DefaultName, cancel);
        if (!canPush)
        {
            log?.Invoke("Forking " + DefaultRepo);
            await Fork(http, cancel);
            headRepo = user + "/" + DefaultName;
            await Task.Delay(2000, cancel);
        }

        var sha = await DefaultBranchSha(http, headRepo, cancel);
        await CreateBranch(http, headRepo, branch, sha, cancel);
        log?.Invoke("branch " + branch);

        foreach (var file in Directory.GetFiles(packageDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(packageDir, file).Replace('\\', '/');
            var apiPath = "mods/" + info.Id + "/" + rel;
            log?.Invoke("upload " + apiPath);
            await PutFile(http, headRepo, branch, apiPath, File.ReadAllBytes(file), "Add " + info.Id + " " + rel, cancel);
        }

        var head = canPush ? branch : user + ":" + branch;
        var body =
            "Original RoweMod clothing item.\n\n" +
            "- [ ] This is my work (or I have rights to share it)\n" +
            "- [ ] No ripped game meshes or dumps/\n" +
            "- [ ] Cooked assets are only this -mod item\n\n" +
            "id: " + info.Id + "\nrow: " + info.Row + "\nslot: " + info.Slot;
        var prUrl = await OpenPr(http, title, head, body, cancel);
        log?.Invoke("PR " + prUrl);
        return prUrl;
    }

    async Task<List<string>> ListModFilesAsync(HttpClient http, CatalogEntry entry, CancellationToken cancel)
    {
        var files = new List<string>();
        try
        {
            var filesJson = await http.GetStringAsync(RawRoot + entry.Path.TrimEnd('/') + "/files.json", cancel);
            using var doc = JsonDocument.Parse(filesJson);
            if (doc.RootElement.TryGetProperty("files", out var arr))
            {
                foreach (var x in arr.EnumerateArray())
                {
                    var s = x.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && s != "files.json")
                        files.Add(s);
                }
            }
        }
        catch (HttpRequestException)
        {
            // guess below
        }
        return files;
    }

    static IEnumerable<string> GuessFiles(CatalogEntry entry)
    {
        yield return "mod.json";
        yield return "item.json";
        yield return "preview.png";
        yield return "files.json";
    }

    static async Task<string> GetLogin(HttpClient http, CancellationToken cancel)
    {
        using var doc = JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/user", cancel));
        return doc.RootElement.GetProperty("login").GetString() ?? "user";
    }

    static async Task<bool> CanPush(HttpClient http, string owner, string name, CancellationToken cancel)
    {
        try
        {
            using var resp = await http.GetAsync("https://api.github.com/repos/" + owner + "/" + name, cancel);
            if (!resp.IsSuccessStatusCode) return false;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancel));
            return doc.RootElement.TryGetProperty("permissions", out var p)
                && p.TryGetProperty("push", out var push)
                && push.GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    static async Task Fork(HttpClient http, CancellationToken cancel)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/repos/" + DefaultRepo + "/forks");
        var resp = await http.SendAsync(req, cancel);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("Could not fork gallery: " + await resp.Content.ReadAsStringAsync(cancel));
    }

    static async Task<string> DefaultBranchSha(HttpClient http, string repo, CancellationToken cancel)
    {
        foreach (var branch in new[] { "main", "master" })
        {
            using var resp = await http.GetAsync("https://api.github.com/repos/" + repo + "/git/ref/heads/" + branch, cancel);
            if (!resp.IsSuccessStatusCode) continue;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancel));
            return doc.RootElement.GetProperty("object").GetProperty("sha").GetString()
                ?? throw new InvalidOperationException("no sha");
        }
        throw new InvalidOperationException("Gallery repo has no main branch yet.");
    }

    static async Task CreateBranch(HttpClient http, string repo, string branch, string sha, CancellationToken cancel)
    {
        var payload = JsonSerializer.Serialize(new { @ref = "refs/heads/" + branch, sha });
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/repos/" + repo + "/git/refs")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        var resp = await http.SendAsync(req, cancel);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("Could not create branch: " + await resp.Content.ReadAsStringAsync(cancel));
    }

    static async Task PutFile(HttpClient http, string repo, string branch, string path, byte[] bytes, string message, CancellationToken cancel)
    {
        var payload = JsonSerializer.Serialize(new
        {
            message,
            content = Convert.ToBase64String(bytes),
            branch,
        });
        using var req = new HttpRequestMessage(HttpMethod.Put, "https://api.github.com/repos/" + repo + "/contents/" + path.Replace("\\", "/"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        var resp = await http.SendAsync(req, cancel);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("Upload failed " + path + ": " + await resp.Content.ReadAsStringAsync(cancel));
    }

    static async Task<string> OpenPr(HttpClient http, string title, string head, string body, CancellationToken cancel)
    {
        var payload = JsonSerializer.Serialize(new { title, head, @base = "main", body });
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/repos/" + DefaultRepo + "/pulls")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        var resp = await http.SendAsync(req, cancel);
        var text = await resp.Content.ReadAsStringAsync(cancel);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("Could not open PR: " + text);
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("html_url").GetString() ?? "https://github.com/" + DefaultRepo + "/pulls";
    }
}

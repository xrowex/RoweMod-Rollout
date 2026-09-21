using System.Diagnostics;
using System.Text.Json;

namespace RoweMod.Core;

public static class GitHubAuth
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static string? SavedToken()
    {
        try
        {
            var path = AppPaths.GitHubTokenFile;
            if (!File.Exists(path)) return null;
            var file = JsonSerializer.Deserialize<GitHubTokenFile>(File.ReadAllText(path), Json);
            return string.IsNullOrWhiteSpace(file?.Token) ? null : file.Token;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveToken(string token)
    {
        AppPaths.EnsureAppData();
        File.WriteAllText(AppPaths.GitHubTokenFile, JsonSerializer.Serialize(new GitHubTokenFile { Token = token }, Json));
    }

    public static string? TryGhToken()
    {
        try
        {
            var psi = new ProcessStartInfo("gh", "auth token")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd().Trim();
            if (!p.WaitForExit(8000)) return null;
            return p.ExitCode == 0 && output.Length > 10 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    public static string? ResolveToken() => SavedToken() ?? TryGhToken();

    public static async Task<string> DeviceFlowAsync(string clientId, Action<string> prompt, CancellationToken cancel = default)
    {
        using var http = GalleryClient.Http();
        var start = await PostForm(http, "https://github.com/login/device/code",
            new Dictionary<string, string> { ["client_id"] = clientId, ["scope"] = "public_repo" }, cancel);
        using var doc = JsonDocument.Parse(start);
        var root = doc.RootElement;
        var device = root.GetProperty("device_code").GetString() ?? "";
        var userCode = root.GetProperty("user_code").GetString() ?? "";
        var verify = root.GetProperty("verification_uri").GetString() ?? "https://github.com/login/device";
        var interval = root.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 5;
        prompt("Open " + verify + " and enter " + userCode);
        try
        {
            Process.Start(new ProcessStartInfo(verify) { UseShellExecute = true });
        }
        catch { /* browser optional */ }

        while (!cancel.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, interval)), cancel);
            var poll = await PostForm(http, "https://github.com/login/oauth/access_token",
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["device_code"] = device,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                }, cancel);
            using var tok = JsonDocument.Parse(poll);
            if (tok.RootElement.TryGetProperty("access_token", out var at))
            {
                var token = at.GetString() ?? "";
                if (token.Length > 0)
                {
                    SaveToken(token);
                    return token;
                }
            }
            var err = tok.RootElement.TryGetProperty("error", out var e) ? e.GetString() : "";
            if (err is "authorization_pending" or "slow_down") continue;
            throw new InvalidOperationException("GitHub login failed: " + (err ?? poll));
        }
        throw new OperationCanceledException();
    }

    static async Task<string> PostForm(HttpClient http, string url, Dictionary<string, string> fields, CancellationToken cancel)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(fields),
        };
        req.Headers.Accept.ParseAdd("application/json");
        var resp = await http.SendAsync(req, cancel);
        return await resp.Content.ReadAsStringAsync(cancel);
    }
}

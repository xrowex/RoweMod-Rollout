using System.IO.Compression;
using CUE4Parse.Compression;

namespace RolloutExtractor;

static class NativeCodecs
{
    const string ZipUrl = "https://github.com/WorkingRobot/OodleUE/releases/download/2026-06-04-1357/clang-cl-x64-release.zip";

    public static void Initialize(string outDir)
    {
        var dumps = Path.GetFullPath(Path.Combine(outDir, ".."));
        var dest = Path.Combine(dumps, "oo2core_9_win64.dll");
        var found = FindOodle(dumps);
        if (found == null)
        {
            Console.WriteLine("Oodle dll missing. Downloading into dumps/ (gitignored)...");
            try
            {
                if (OodleHelper.DownloadOodleDll(dest) && File.Exists(dest))
                    found = dest;
            }
            catch (Exception ex)
            {
                Console.WriteLine("CUE4Parse Oodle download failed: " + ex.Message);
            }
        }

        if (found == null)
            found = DownloadOodleUe(dumps, dest);

        if (found != null && File.Exists(found))
        {
            OodleHelper.Initialize(found);
            Console.WriteLine("Oodle " + found);
            return;
        }

        throw new FileNotFoundException(
            "Oodle dll not found. Place oo2core_9_win64.dll in dumps/.");
    }

    static string? FindOodle(string dumps)
    {
        foreach (var p in new[]
        {
            Path.Combine(dumps, "oo2core_9_win64.dll"),
            Path.Combine(dumps, "oodle-data-shared.dll"),
            Path.Combine(AppContext.BaseDirectory, "oo2core_9_win64.dll"),
            Path.Combine(AppContext.BaseDirectory, "oodle-data-shared.dll"),
            Path.Combine(Path.GetTempPath(), "oo2core_9_win64.dll"),
        })
        {
            if (File.Exists(p)) return p;
        }
        return null;
    }

    static string? DownloadOodleUe(string dumps, string dest)
    {
        try
        {
            Directory.CreateDirectory(dumps);
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RoweMod-RolloutExtractor");
            Console.WriteLine("GET " + ZipUrl);
            using var resp = http.GetAsync(ZipUrl).GetAwaiter().GetResult();
            resp.EnsureSuccessStatusCode();
            var zipPath = Path.Combine(dumps, "oodle-ue.zip");
            File.WriteAllBytes(zipPath, resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                var name = Path.GetFileName(entry.FullName);
                if (name.Equals("oo2core_9_win64.dll", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("oodle-data-shared.dll", StringComparison.OrdinalIgnoreCase))
                {
                    var target = Path.Combine(dumps, name);
                    entry.ExtractToFile(target, overwrite: true);
                    if (!File.Exists(dest))
                        File.Copy(target, dest, overwrite: false);
                    Console.WriteLine("Extracted " + name + " (" + new FileInfo(target).Length + ")");
                    return File.Exists(dest) ? dest : target;
                }
            }
            Console.WriteLine("Oodle zip had no dll. Entries:");
            foreach (var entry in zip.Entries.Take(20))
                Console.WriteLine("  " + entry.FullName);
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("OodleUE download failed: " + ex.Message);
            return null;
        }
    }
}

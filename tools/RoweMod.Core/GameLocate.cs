using System.Text.Json;
using System.Text.RegularExpressions;

namespace RoweMod.Core;

public static class GameLocate
{
    public static string? FindPaks()
    {
        var env = Environment.GetEnvironmentVariable("ROWE_GAME_PAKS");
        if (LooksLike(env)) return Path.GetFullPath(env!);

        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.SettingsFile));
                foreach (var name in new[] { "GamePaks", "gamePaks" })
                {
                    if (doc.RootElement.TryGetProperty(name, out var p))
                    {
                        var path = p.GetString();
                        if (LooksLike(path)) return Path.GetFullPath(path!);
                    }
                }
            }
        }
        catch { /* ignore */ }

        const string rel = @"steamapps\common\RolloutInline\RollerSkate\Content\Paks";
        foreach (var lib in SteamLibraries())
        {
            var paks = Path.Combine(lib, rel);
            if (LooksLike(paks)) return Path.GetFullPath(paks);
        }

        var fallback = @"C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks";
        return LooksLike(fallback) ? fallback : null;
    }

    static IEnumerable<string> SteamLibraries()
    {
        var steam = SteamInstall();
        if (steam == null) yield break;
        yield return steam;
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        foreach (var line in File.ReadLines(vdf))
        {
            var m = Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"");
            if (!m.Success) continue;
            var p = m.Groups[1].Value.Replace(@"\\", @"\");
            if (Directory.Exists(p)) yield return Path.GetFullPath(p);
        }
    }

    static string? SteamInstall()
    {
        foreach (var path in new[]
        {
            ReadReg(Microsoft.Win32.Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath", "InstallPath"),
            ReadReg(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", "SteamPath"),
            ReadReg(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath", "SteamPath"),
        })
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                return Path.GetFullPath(path);
        }

        foreach (var guess in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
            @"C:\Program Files (x86)\Steam",
        })
        {
            if (File.Exists(Path.Combine(guess, "steam.exe")))
                return Path.GetFullPath(guess);
        }
        return null;
    }

    static string ReadReg(Microsoft.Win32.RegistryKey hive, string subkey, params string[] names)
    {
        try
        {
            using var key = hive.OpenSubKey(subkey);
            if (key == null) return "";
            foreach (var name in names)
            {
                if (key.GetValue(name) is string path && !string.IsNullOrWhiteSpace(path))
                    return path.Replace('/', '\\');
            }
        }
        catch { /* ignore */ }
        return "";
    }

    static bool LooksLike(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
        && (Directory.EnumerateFiles(folder, "*.utoc").Any() || Directory.EnumerateFiles(folder, "*.pak").Any());
}

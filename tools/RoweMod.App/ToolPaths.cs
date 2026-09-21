using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RoweMod.App;

sealed class DetectedTools
{
    public required string Repo { get; init; }
    public string? GamePaks { get; init; }
    public string? Blender { get; init; }
    public string? UnrealEditor { get; init; }
    public bool HasDotnet { get; init; }
    public bool HasRetoc { get; init; }
    public bool HasPulledClothing { get; init; }
    public string? OverlayUtoc { get; init; }
    public DateTime? LastExport { get; init; }
    public DateTime? LastCook { get; init; }
    public DateTime? LastTarget { get; init; }

    public string RetocPath => Path.Combine(Repo, "tools", "retoc", "retoc.exe");
    public string BootstrapScript => Path.Combine(Repo, "tools", "bootstrap.ps1");
    public string PackScript => Path.Combine(Repo, "tools", "pack_mod.ps1");
    public string CookScript => Path.Combine(Repo, "ue", "cook_mod.ps1");
    public string ExportScript => Path.Combine(Repo, "art", "export_shirt.py");
    public string PullScript => Path.Combine(Repo, "tools", "pull_clothing.ps1");
    public string PulledClothingDir => Path.Combine(Repo, "dumps", "game-clothing");
    public string GamebindGlb => Path.Combine(Repo, "art", "tshirt-baggy-male.gamebind.glb");
}

static class ToolPaths
{
    public static string RepoRoot { get; } = DtPatcher.Program.DiscoverRepo();

    public static DetectedTools Detect()
    {
        var repo = RepoRoot;
        var paks = FindGamePaks();
        var target = MeshWork.LoadOrDefault(repo);
        var export = File.Exists(target.Gamebind)
            ? target.Gamebind
            : Path.Combine(repo, "art", "tshirt-baggy-male.gamebind.glb");
        var cookedRel = (string.IsNullOrWhiteSpace(target.MeshDir)
                ? "/Game/MainFolder/Character/upper/tshirt-baggy"
                : target.MeshDir)
            .Replace("/Game/", "")
            .Replace('/', Path.DirectorySeparatorChar);
        var cooked = Path.Combine(
            repo,
            "ue", "RollerSkate", "Saved", "Cooked", "Windows", "RollerSkate", "Content",
            cookedRel,
            (string.IsNullOrWhiteSpace(target.MeshName) ? "tshirt-baggy-male" : target.MeshName) + ".uasset");
        string? overlay = null;
        if (paks != null)
        {
            var utoc = Path.Combine(paks, "RollerSkate-Windows_P.utoc");
            if (File.Exists(utoc)) overlay = utoc;
        }

        return new DetectedTools
        {
            Repo = repo,
            GamePaks = paks,
            Blender = FindBlender51(),
            UnrealEditor = FindUnreal54(),
            HasDotnet = HasDotnet8(),
            HasRetoc = File.Exists(Path.Combine(repo, "tools", "retoc", "retoc.exe")),
            HasPulledClothing = File.Exists(Path.Combine(repo, "dumps", "game-clothing", "manifest.json")),
            OverlayUtoc = overlay,
            LastExport = File.Exists(export) ? File.GetLastWriteTime(export) : null,
            LastCook = File.Exists(cooked) ? File.GetLastWriteTime(cooked) : null,
            LastTarget = File.Exists(MeshWork.TargetPath(repo)) ? File.GetLastWriteTime(MeshWork.TargetPath(repo)) : null,
        };
    }

    public static string? FindGamePaks()
    {
        var env = Environment.GetEnvironmentVariable("ROWE_GAME_PAKS");
        if (!string.IsNullOrWhiteSpace(env) && LooksLikePaks(env))
            return Path.GetFullPath(env);

        var saved = LocalSettings.Load().GamePaks;
        if (!string.IsNullOrWhiteSpace(saved) && LooksLikePaks(saved))
            return Path.GetFullPath(saved);

        const string rel = @"steamapps\common\RolloutInline\RollerSkate\Content\Paks";
        foreach (var lib in SteamLibraries())
        {
            var paks = Path.Combine(lib, rel);
            if (LooksLikePaks(paks))
                return Path.GetFullPath(paks);
        }

        var fallback = @"C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks";
        return LooksLikePaks(fallback) ? Path.GetFullPath(fallback) : null;
    }

    public static bool LooksLikePaks(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;
        return Directory.EnumerateFiles(folder, "*.utoc").Any()
            || Directory.EnumerateFiles(folder, "*.pak").Any();
    }

    public static string? ResolveGamePaksFromFolder(string picked)
    {
        if (string.IsNullOrWhiteSpace(picked) || !Directory.Exists(picked)) return null;
        var root = Path.GetFullPath(picked);
        foreach (var candidate in new[]
        {
            root,
            Path.Combine(root, "Paks"),
            Path.Combine(root, "Content", "Paks"),
            Path.Combine(root, "RollerSkate", "Content", "Paks"),
            Path.Combine(root, "RolloutInline", "RollerSkate", "Content", "Paks"),
            Path.Combine(root, "steamapps", "common", "RolloutInline", "RollerSkate", "Content", "Paks"),
        })
        {
            if (LooksLikePaks(candidate)) return Path.GetFullPath(candidate);
        }

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root, "Paks", SearchOption.AllDirectories))
            {
                if (dir.Contains("RollerSkate", StringComparison.OrdinalIgnoreCase) && LooksLikePaks(dir))
                    return Path.GetFullPath(dir);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // stay with explicit candidates
        }
        return null;
    }

    public static void RememberGamePaks(string paks)
    {
        Environment.SetEnvironmentVariable("ROWE_GAME_PAKS", paks);
        var settings = LocalSettings.Load();
        settings.GamePaks = paks;
        settings.Save();
    }

    public static string? FindBlender51()
    {
        var env = Environment.GetEnvironmentVariable("ROWE_BLENDER");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return Path.GetFullPath(env);

        var guesses = new[]
        {
            @"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe",
            @"C:\Program Files (x86)\Blender Foundation\Blender 5.1\blender.exe",
            @"D:\Program Files\Blender Foundation\Blender 5.1\blender.exe",
        };
        foreach (var g in guesses)
        {
            if (File.Exists(g)) return g;
        }

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var root = Path.Combine(pf, "Blender Foundation");
        if (!Directory.Exists(root)) return null;
        foreach (var dir in Directory.GetDirectories(root, "Blender 5.1*"))
        {
            var exe = Path.Combine(dir, "blender.exe");
            if (File.Exists(exe)) return exe;
        }
        return null;
    }

    public static string? FindUnreal54()
    {
        var env = Environment.GetEnvironmentVariable("ROWE_UE54");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return Path.GetFullPath(env);

        var guesses = new[]
        {
            @"E:\unreal\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
            @"E:\EpicGames\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
            @"C:\Program Files\Epic Games\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
            @"D:\Epic Games\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
            @"C:\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
        };
        foreach (var g in guesses)
        {
            if (File.Exists(g)) return g;
        }

        foreach (var root in new[] { @"E:\unreal", @"E:\EpicGames", @"C:\Program Files\Epic Games", @"D:\Epic Games" })
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var exe in Directory.EnumerateFiles(root, "UnrealEditor.exe", SearchOption.AllDirectories))
                {
                    if (exe.Contains(@"UE_5.4\", StringComparison.OrdinalIgnoreCase))
                        return exe;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // skip locked Epic trees
            }
        }
        return null;
    }

    public static bool HasDotnet8()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(4000);
            return output.Contains("Microsoft.WindowsDesktop.App 8.") ||
                   output.Contains("Microsoft.NETCore.App 8.");
        }
        catch
        {
            return false;
        }
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
            if (Directory.Exists(p)) yield return p;
        }
    }

    static string? SteamInstall()
    {
        try
        {
            using var cu = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = cu?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                return Path.GetFullPath(path);
        }
        catch { /* no steam user key */ }

        try
        {
            using var lm = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            var path = lm?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                return Path.GetFullPath(path);
        }
        catch { /* no steam machine key */ }

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
}

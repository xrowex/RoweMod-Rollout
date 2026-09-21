using System.Diagnostics;
using System.Text.Json;
using RoweMod.Core;

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
        if (LooksLikePaks(env))
            return Path.GetFullPath(env!);

        var saved = LocalSettings.Load().GamePaks;
        if (LooksLikePaks(saved))
            return Path.GetFullPath(saved!);

        return SteamLocate.FindRolloutPaks();
    }

    public static bool LooksLikePaks(string? folder) => SteamLocate.LooksLikePaks(folder);

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
            Path.Combine(root, "Rollout Inline", "RollerSkate", "Content", "Paks"),
            Path.Combine(root, "steamapps", "common", "RolloutInline", "RollerSkate", "Content", "Paks"),
            Path.Combine(root, "steamapps", "common", "Rollout Inline", "RollerSkate", "Content", "Paks"),
        })
        {
            if (LooksLikePaks(candidate)) return Path.GetFullPath(candidate);
        }

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root, "Paks", SearchOption.AllDirectories))
            {
                if (LooksLikePaks(dir)) return Path.GetFullPath(dir);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // stay with explicit candidates
        }
        catch (DirectoryNotFoundException)
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

    public static void RememberUnrealEditor(string editor)
    {
        Environment.SetEnvironmentVariable("ROWE_UE54", editor);
        var settings = LocalSettings.Load();
        settings.UnrealEditor = editor;
        settings.Save();
    }

    public static string? ResolveUnrealFromPath(string picked)
    {
        if (string.IsNullOrWhiteSpace(picked)) return null;

        if (File.Exists(picked))
        {
            if (picked.EndsWith("UnrealEditor.exe", StringComparison.OrdinalIgnoreCase) && LooksLikeUe54(picked))
                return Path.GetFullPath(picked);
            return null;
        }

        if (!Directory.Exists(picked)) return null;
        var root = Path.GetFullPath(picked);
        foreach (var candidate in new[]
        {
            Path.Combine(root, "Engine", "Binaries", "Win64", "UnrealEditor.exe"),
            Path.Combine(root, "Binaries", "Win64", "UnrealEditor.exe"),
            Path.Combine(root, "UnrealEditor.exe"),
        })
        {
            if (File.Exists(candidate) && LooksLikeUe54(candidate))
                return Path.GetFullPath(candidate);
        }

        try
        {
            foreach (var exe in Directory.EnumerateFiles(root, "UnrealEditor.exe", SearchOption.AllDirectories))
            {
                if (LooksLikeUe54(exe))
                    return Path.GetFullPath(exe);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // stay with explicit candidates
        }
        return null;
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
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env) && LooksLikeUe54(env))
            return Path.GetFullPath(env);

        var saved = LocalSettings.Load().UnrealEditor;
        if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved) && LooksLikeUe54(saved))
            return Path.GetFullPath(saved);

        foreach (var fromLauncher in EpicInstalledEngines())
        {
            if (File.Exists(fromLauncher) && LooksLikeUe54(fromLauncher))
                return Path.GetFullPath(fromLauncher);
        }

        foreach (var fromRegistry in EpicRegisteredBuilds())
        {
            if (File.Exists(fromRegistry) && LooksLikeUe54(fromRegistry))
                return Path.GetFullPath(fromRegistry);
        }

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
            if (File.Exists(g) && LooksLikeUe54(g)) return g;
        }

        foreach (var root in new[]
        {
            @"E:\unreal",
            @"E:\EpicGames",
            @"C:\Program Files\Epic Games",
            @"D:\Epic Games",
            @"D:\Unreal",
            @"C:\Unreal",
        })
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var exe in Directory.EnumerateFiles(root, "UnrealEditor.exe", SearchOption.AllDirectories))
                {
                    if (LooksLikeUe54(exe))
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

    public static bool LooksLikeUe54(string editorExe)
    {
        if (string.IsNullOrWhiteSpace(editorExe) || !File.Exists(editorExe)) return false;
        if (editorExe.Contains(@"UE_5.4\", StringComparison.OrdinalIgnoreCase)
            || editorExe.Contains(@"UE_5.4/", StringComparison.OrdinalIgnoreCase))
            return true;

        var version = FindBuildVersion(editorExe);
        if (version == null) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(version));
            var root = doc.RootElement;
            var major = root.TryGetProperty("MajorVersion", out var maj) ? maj.GetInt32() : 0;
            var minor = root.TryGetProperty("MinorVersion", out var min) ? min.GetInt32() : -1;
            return major == 5 && minor == 4;
        }
        catch
        {
            return false;
        }
    }

    static string? FindBuildVersion(string editorExe)
    {
        // ...\Engine\Binaries\Win64\UnrealEditor.exe → ...\Engine\Build\Build.version
        var win64 = Path.GetDirectoryName(editorExe);
        var binaries = win64 != null ? Path.GetDirectoryName(win64) : null;
        var engine = binaries != null ? Path.GetDirectoryName(binaries) : null;
        if (engine == null) return null;
        var path = Path.Combine(engine, "Build", "Build.version");
        return File.Exists(path) ? path : null;
    }

    static IEnumerable<string> EpicInstalledEngines()
    {
        var dat = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");
        if (!File.Exists(dat)) yield break;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(dat));
        }
        catch
        {
            yield break;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("InstallationList", out var list)
                || list.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (var item in list.EnumerateArray())
            {
                var app = item.TryGetProperty("AppName", out var an) ? an.GetString() : null;
                var artifact = item.TryGetProperty("ArtifactId", out var ar) ? ar.GetString() : null;
                var loc = item.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
                if (string.IsNullOrWhiteSpace(loc)) continue;

                var name = (app ?? "") + " " + (artifact ?? "");
                var looks54 = name.Contains("UE_5.4", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("5.4", StringComparison.OrdinalIgnoreCase)
                    || loc.Contains("UE_5.4", StringComparison.OrdinalIgnoreCase);
                if (!looks54) continue;

                var exe = Path.Combine(loc, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
                if (File.Exists(exe))
                    yield return exe;
            }
        }
    }

    static IEnumerable<string> EpicRegisteredBuilds()
    {
        string?[] keys =
        {
            @"Software\Epic Games\Unreal Engine\Builds",
            @"Software\EpicGames\Unreal Engine\Builds",
        };
        foreach (var keyPath in keys)
        {
            Microsoft.Win32.RegistryKey? key = null;
            try
            {
                key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
            }
            catch
            {
                continue;
            }
            if (key == null) continue;
            using (key)
            {
                foreach (var name in key.GetValueNames())
                {
                    if (key.GetValue(name) is not string path || string.IsNullOrWhiteSpace(path))
                        continue;
                    var exe = Path.Combine(path, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
                    if (File.Exists(exe))
                        yield return exe;
                }
            }
        }
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

}

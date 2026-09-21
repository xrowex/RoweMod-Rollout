using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RoweMod.Core;

/// <summary>
/// Find Steam libraries and Rollout Inline Content\Paks on any drive / library layout.
/// </summary>
public static class SteamLocate
{
    public const string AppId = "4464990";

    public static string? FindRolloutPaks()
    {
        foreach (var lib in Libraries())
        {
            var fromManifest = FromAppManifest(lib);
            if (fromManifest != null) return fromManifest;

            foreach (var folder in KnownInstallFolderNames())
            {
                var paks = Path.Combine(lib, "steamapps", "common", folder, "RollerSkate", "Content", "Paks");
                if (LooksLikePaks(paks)) return Path.GetFullPath(paks);
            }
        }

        var fallback = @"C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks";
        return LooksLikePaks(fallback) ? Path.GetFullPath(fallback) : null;
    }

    public static IEnumerable<string> Libraries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in CandidateSteamRoots())
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            var full = Path.GetFullPath(root);
            if (!seen.Add(full)) continue;
            yield return full;

            var vdf = Path.Combine(full, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (var line in File.ReadLines(vdf))
            {
                var m = Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                var p = m.Groups[1].Value.Replace(@"\\", @"\").Replace('/', '\\');
                if (!Directory.Exists(p)) continue;
                var lib = Path.GetFullPath(p);
                if (seen.Add(lib))
                    yield return lib;
            }
        }
    }

    static IEnumerable<string> CandidateSteamRoots()
    {
        foreach (var path in RegistrySteamPaths())
        {
            if (!string.IsNullOrWhiteSpace(path)) yield return path;
        }

        foreach (var path in RunningSteamPaths())
        {
            if (!string.IsNullOrWhiteSpace(path)) yield return path;
        }

        foreach (var guess in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"C:\Steam",
            @"D:\Steam",
            @"D:\SteamLibrary",
            @"E:\Steam",
            @"E:\SteamLibrary",
        })
        {
            yield return guess;
        }

        foreach (var drive in SafeFixedDrives())
        {
            foreach (var name in new[] { "SteamLibrary", "Steam", @"Program Files (x86)\Steam", @"Program Files\Steam", @"Games\Steam", @"Games\SteamLibrary" })
            {
                yield return Path.Combine(drive, name);
            }
        }
    }

    static IEnumerable<string> SafeFixedDrives()
    {
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch { yield break; }

        foreach (var d in drives)
        {
            try
            {
                if (!d.IsReady || d.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                yield return d.RootDirectory.FullName;
            }
            catch
            {
                // skip locked / optical
            }
        }
    }

    static IEnumerable<string> RunningSteamPaths()
    {
        Process[] procs;
        try { procs = Process.GetProcessesByName("steam"); }
        catch { yield break; }

        foreach (var p in procs)
        {
            string? file = null;
            try { file = p.MainModule?.FileName; }
            catch { /* access denied */ }
            finally { try { p.Dispose(); } catch { /* ignore */ } }
            if (string.IsNullOrWhiteSpace(file)) continue;
            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrWhiteSpace(dir)) yield return dir;
        }
    }

    static IEnumerable<string> RegistrySteamPaths()
    {
        yield return ReadReg(Microsoft.Win32.Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath", "InstallPath");
        yield return ReadReg(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", "SteamPath");
        yield return ReadReg(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath", "SteamPath");
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

    static string? FromAppManifest(string libraryRoot)
    {
        var manifest = Path.Combine(libraryRoot, "steamapps", "appmanifest_" + AppId + ".acf");
        if (!File.Exists(manifest)) return null;

        string? installdir = null;
        try
        {
            foreach (var line in File.ReadLines(manifest))
            {
                var m = Regex.Match(line, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    installdir = m.Groups[1].Value;
                    break;
                }
            }
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(installdir)) return null;
        var common = Path.Combine(libraryRoot, "steamapps", "common", installdir);
        foreach (var candidate in new[]
        {
            Path.Combine(common, "RollerSkate", "Content", "Paks"),
            Path.Combine(common, "Content", "Paks"),
            Path.Combine(common, "Paks"),
        })
        {
            if (LooksLikePaks(candidate)) return Path.GetFullPath(candidate);
        }

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(common, "Paks", SearchOption.AllDirectories))
            {
                if (LooksLikePaks(dir)) return Path.GetFullPath(dir);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // ignore
        }
        catch (DirectoryNotFoundException)
        {
            // ignore
        }
        return null;
    }

    static IEnumerable<string> KnownInstallFolderNames()
    {
        yield return "RolloutInline";
        yield return "Rollout Inline";
        yield return "RolloutInlineDemo";
        yield return "RollerSkate";
    }

    public static bool LooksLikePaks(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
        && (Directory.EnumerateFiles(folder, "*.utoc").Any() || Directory.EnumerateFiles(folder, "*.pak").Any());
}

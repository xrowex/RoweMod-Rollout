using System.Diagnostics;
using System.Text.Json;
using DtPatcher;

namespace RoweMod.Core;

public static class ModMerge
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static int Pack(string repo, string gamePaks, Action<string>? log = null)
    {
        if (!Directory.Exists(gamePaks))
            throw new InvalidOperationException("Game Paks folder not found.");
        var retoc = Path.Combine(repo, "tools", "retoc", "retoc.exe");
        if (!File.Exists(retoc))
            throw new InvalidOperationException("retoc missing. Run Setup first.");

        foreach (var p in Process.GetProcessesByName("RollerSkate"))
        {
            log?.Invoke("Stopping " + p.ProcessName);
            p.Kill(true);
        }

        var extras = DtPatcher.Program.DiscoverWorkshopItems(repo).ToArray();
        log?.Invoke("Patching " + extras.Length + " subscribed item(s) plus local items.");
        var patched = DtPatcher.Program.PatchAllItems(repo, extras, log);
        if (patched != 0)
            throw new InvalidOperationException("DtPatcher failed (" + patched + ")");

        var staging = Path.Combine(repo, "dumps", "mod-staging");
        if (Directory.Exists(staging))
            Directory.Delete(staging, recursive: true);
        var stageContent = Path.Combine(staging, "RollerSkate", "Content");
        Directory.CreateDirectory(stageContent);

        var dtDst = Path.Combine(stageContent, "MainFolder", "UI", "customization", "data");
        Directory.CreateDirectory(dtDst);
        var patchedDir = Path.Combine(repo, "dumps", "patched");
        if (!Directory.Exists(patchedDir))
            throw new InvalidOperationException("missing dumps/patched. Run Setup first.");
        foreach (var file in Directory.GetFiles(patchedDir))
        {
            File.Copy(file, Path.Combine(dtDst, Path.GetFileName(file)), overwrite: true);
            log?.Invoke("staged " + Path.GetFileName(file));
        }

        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var itemPath in LocalItems(repo).Concat(extras))
            StageItem(repo, stageContent, itemPath, missing, seen, log);

        if (missing.Count > 0)
        {
            foreach (var path in missing.Distinct())
                log?.Invoke("skip unpackable " + path);
        }

        var built = Path.Combine(repo, "dumps", "overlay", "RollerSkate-Windows_P.utoc");
        Directory.CreateDirectory(Path.GetDirectoryName(built)!);
        foreach (var leftover in Directory.GetFiles(Path.GetDirectoryName(built)!, "RollerSkate-Windows_P.*"))
            File.Delete(leftover);

        log?.Invoke("Packing overlay");
        var code = RunRetoc(retoc, staging, built, log);
        if (code != 0)
            throw new InvalidOperationException("retoc failed (" + code + ")");

        var mods = Path.Combine(gamePaks, "~mods");
        Directory.CreateDirectory(mods);
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(built)!, "RollerSkate-Windows_P.*"))
        {
            File.Copy(file, Path.Combine(gamePaks, Path.GetFileName(file)), overwrite: true);
            File.Copy(file, Path.Combine(mods, Path.GetFileName(file)), overwrite: true);
        }
        log?.Invoke("OVERLAY " + Path.Combine(gamePaks, "RollerSkate-Windows_P.utoc"));
        return 0;
    }

    public static void LaunchGame()
    {
        Process.Start(new ProcessStartInfo("steam://run/4464990") { UseShellExecute = true });
    }

    static IEnumerable<string> LocalItems(string repo)
    {
        var dir = Path.Combine(repo, "items");
        if (!Directory.Exists(dir)) yield break;
        foreach (var json in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
            yield return json;
    }

    static void StageItem(string repo, string stageContent, string itemJson, List<string> missing, HashSet<string> seen, Action<string>? log)
    {
        ItemSpec spec;
        try { spec = ModPackage.ReadItem(itemJson); }
        catch { return; }
        if (spec.Sample) return;
        if (!seen.Add(spec.Row)) return;
        if (!ModPackage.CanPack(repo, spec, out var why))
        {
            log?.Invoke("skip " + spec.Row + " (" + why + ")");
            return;
        }
        foreach (var gamePath in spec.AssetPaths())
        {
            if (!StageGamePath(repo, stageContent, itemJson, gamePath, log))
                missing.Add(spec.Row + " " + gamePath);
        }
    }

    static bool StageGamePath(string repo, string stageContent, string itemJson, string gamePath, Action<string>? log)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !gamePath.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase))
            return true;
        var rel = gamePath["/Game/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var parent = Path.GetDirectoryName(rel) ?? "";
        var dstDir = Path.Combine(stageContent, parent);
        var files = ModPackage.FindCookedFiles(repo, gamePath, Path.GetDirectoryName(itemJson)).ToList();
        if (files.Count == 0) return false;
        Directory.CreateDirectory(dstDir);
        foreach (var file in files)
        {
            var dest = Path.Combine(dstDir, Path.GetFileName(file));
            var existed = File.Exists(dest);
            File.Copy(file, dest, overwrite: true);
            if (!existed)
                log?.Invoke("staged " + Path.GetFileName(file));
        }
        return true;
    }

    static int RunRetoc(string retoc, string staging, string outUtoc, Action<string>? log)
    {
        var psi = new ProcessStartInfo(retoc)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("to-zen");
        psi.ArgumentList.Add("--version");
        psi.ArgumentList.Add("UE5_4");
        psi.ArgumentList.Add(staging);
        psi.ArgumentList.Add(outUtoc);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start retoc");
        p.OutputDataReceived += (_, e) => { if (e.Data != null) log?.Invoke(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) log?.Invoke(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }
}

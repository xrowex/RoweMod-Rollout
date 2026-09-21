using System.Diagnostics;
using System.Text;

namespace RoweMod.App;

/// <summary>
/// Pulls the latest commits when this checkout is a git clone of RoweMod-Rollout.
/// </summary>
static class RepoUpdate
{
    public static string? TryPull(string repo)
    {
        if (string.IsNullOrWhiteSpace(repo) || !Directory.Exists(Path.Combine(repo, ".git")))
            return null;
        if (!HasGit())
            return "git not on PATH — skip auto-update";

        try
        {
            var fetch = RunGit(repo, "fetch --quiet origin");
            if (fetch.ExitCode != 0)
                return "update check failed: " + Truncate(fetch.Error);

            var behind = RunGit(repo, "rev-list --count HEAD..@{upstream}");
            if (behind.ExitCode != 0)
            {
                // No upstream tracking — try origin/master then origin/main.
                var pull = TryFastForward(repo, "origin/master")
                    ?? TryFastForward(repo, "origin/main");
                return pull;
            }

            if (!int.TryParse(behind.Output.Trim(), out var count) || count <= 0)
                return "up to date";

            var ff = RunGit(repo, "pull --ff-only --quiet");
            if (ff.ExitCode != 0)
                return "update blocked (local changes?): " + Truncate(ff.Error);
            return "updated (" + count + " commit" + (count == 1 ? "" : "s") + ")";
        }
        catch (Exception ex)
        {
            return "update skipped: " + ex.Message;
        }
    }

    static string? TryFastForward(string repo, string refName)
    {
        var check = RunGit(repo, "rev-parse --verify " + refName);
        if (check.ExitCode != 0) return null;
        var count = RunGit(repo, "rev-list --count HEAD.." + refName);
        if (count.ExitCode != 0 || !int.TryParse(count.Output.Trim(), out var n) || n <= 0)
            return "up to date";
        var ff = RunGit(repo, "merge --ff-only --quiet " + refName);
        if (ff.ExitCode != 0)
            return "update blocked (local changes?): " + Truncate(ff.Error);
        return "updated (" + n + " commit" + (n == 1 ? "" : "s") + ")";
    }

    static bool HasGit()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(3000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    static (int ExitCode, string Output, string Error) RunGit(string repo, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("git failed to start");
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        if (!p.WaitForExit(60_000))
        {
            try { p.Kill(true); } catch { /* ignore */ }
            return (-1, stdout.ToString(), "git timed out");
        }
        return (p.ExitCode, stdout.ToString(), stderr.ToString());
    }

    static string Truncate(string text)
    {
        var t = (text ?? "").Trim().Replace('\r', ' ').Replace('\n', ' ');
        return t.Length <= 160 ? t : t[..157] + "...";
    }
}

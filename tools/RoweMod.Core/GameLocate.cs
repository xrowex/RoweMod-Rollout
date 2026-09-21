using System.Text.Json;

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

        var fallback = @"C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks";
        return LooksLike(fallback) ? fallback : null;
    }

    static bool LooksLike(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
        && (Directory.EnumerateFiles(folder, "*.utoc").Any() || Directory.EnumerateFiles(folder, "*.pak").Any());
}

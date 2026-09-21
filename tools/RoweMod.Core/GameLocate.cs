using System.Text.Json;

namespace RoweMod.Core;

public static class GameLocate
{
    public static string? FindPaks()
    {
        var env = Environment.GetEnvironmentVariable("ROWE_GAME_PAKS");
        if (SteamLocate.LooksLikePaks(env)) return Path.GetFullPath(env!);

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
                        if (SteamLocate.LooksLikePaks(path)) return Path.GetFullPath(path!);
                    }
                }
            }
        }
        catch { /* ignore */ }

        return SteamLocate.FindRolloutPaks();
    }
}

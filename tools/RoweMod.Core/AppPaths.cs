namespace RoweMod.Core;

public static class AppPaths
{
    public static string AppData =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoweMod");

    public static string SettingsFile => Path.Combine(AppData, "settings.json");
    public static string SubscriptionsFile => Path.Combine(AppData, "subscriptions.json");
    public static string GitHubTokenFile => Path.Combine(AppData, "github.json");

    public static string WorkshopRoot(string repo) => Path.Combine(repo, "dumps", "workshop");
    public static string WorkshopMod(string repo, string id) => Path.Combine(WorkshopRoot(repo), id);

    public static void EnsureAppData() => Directory.CreateDirectory(AppData);
}

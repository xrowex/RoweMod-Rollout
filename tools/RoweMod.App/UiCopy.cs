namespace RoweMod.App;

/// <summary>
/// Where copy lives. One job per surface — never say the same thing twice.
/// Next     — green card, always on. The one click to do.
/// Hover    — appears only while a toolbar button is hovered.
/// Disabled — native tooltip on workshop actions that cannot run. Not used on the toolbar.
/// Log      — job output and errors. Not instructions.
/// Status   — timestamps. Not instructions.
/// Chips    — binary state.
/// Banner   — which workshop this is.
/// Hint     — last action in a workshop. Hidden when idle.
/// Empty    — list when there is nothing to show.
/// </summary>
static class UiCopy
{
    public readonly record struct Next(string Title, string Body, bool Browse);
    public readonly record struct Hover(string Title, string Body);

    public static Next NextStep(DetectedTools t)
    {
        if (t.GamePaks is null)
            return new("Find the game", "Install on Steam, or browse to Content\\Paks.", true);
        if (!t.HasDotnet)
            return new("Install .NET 8", "SDK from Microsoft, then reopen RoweMod.", false);
        if (!t.HasRetoc)
            return new("Click Setup", "Finds the game and copies the menus.", false);
        if (CookPending(t) && t.UnrealEditor is null)
            return new("Install Unreal 5.4.4", "A PNG or mesh has to cook before Play.", false);
        if (CookPending(t))
            return new("Click Cook", "Unreal has to cook before Play.", false);
        if (t.OverlayUtoc is null)
            return new("Click Play", "Puts the baggy tee and Rowe jeans in the game.", false);
        return new("Make something", "Clothing, Skates, or Gallery — then Play.", false);
    }

    public static Hover? ButtonHover(string? id, DetectedTools t)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var setupDone = t.HasRetoc && t.GamePaks != null;
        return id switch
        {
            "setup" when setupDone => new("Setup", "Already done."),
            "setup" => new("Setup", "Finds the game. Once."),
            "clothing" => new("Clothing", "Paint a PNG or make a new mesh."),
            "skates" => new("Skates", "Boots, frames, wheels."),
            "gallery" => new("Gallery", "Subscribe, then Play. Submit is a PR."),
            "cook" when t.UnrealEditor is null => new("Cook", "Needs Unreal 5.4.4."),
            "cook" => new("Cook", "Prepares PNG or mesh. Then Play."),
            "pack" when t.GamePaks is null => new("Play", "Needs the Steam game."),
            "pack" when !t.HasRetoc => new("Play", "Click Setup first."),
            "pack" => new("Play", "Writes the overlay and launches."),
            _ => null,
        };
    }

    public static string WorkshopBanner(WorkshopDomain domain, WorkshopTab tab) =>
        (domain, tab) switch
        {
            (WorkshopDomain.Skates, WorkshopTab.Paint) => "Paint. Wheels are texture-only.",
            (WorkshopDomain.Skates, _) => "New frame or boot.",
            (_, WorkshopTab.Paint) => "Paint a PNG, then Play.",
            _ => "New mesh on the skeleton.",
        };

    public static string WorkshopEmpty(WorkshopDomain domain, WorkshopTab tab, bool pulled)
    {
        if (tab == WorkshopTab.Paint)
            return pulled ? "No paint files yet." : "Get from game.";
        return pulled ? "" : "Get from game first.";
    }

    public static string GalleryBanner => "Subscribe, then Play. Submit is a GitHub PR.";
    public static string GalleryEmpty => "No mods in the catalog yet.";

    public static bool CookPending(DetectedTools t)
    {
        DateTime? newest = t.LastExport;
        if (t.LastTarget is DateTime tgt && (newest is null || tgt > newest))
            newest = tgt;
        var mesh = newest is DateTime n && (t.LastCook is null || n > t.LastCook.Value.AddSeconds(2));
        return mesh || TextureMods.NeedsCook(t.Repo);
    }
}

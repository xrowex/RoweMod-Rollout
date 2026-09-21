namespace RoweMod.App;

/// <summary>
/// Where copy lives. One job per surface — never say the same thing twice.
/// Next     — green card + CTA. Does the next step; does not point elsewhere.
/// Steps    — Game → Setup → (Cook) → Play. Layout shows progress.
/// Hover    — appears only while a toolbar button is hovered.
/// Log      — job output and errors. Not instructions.
/// Status   — timestamps. Not instructions.
/// Banner   — which workshop this is.
/// </summary>
static class UiCopy
{
    public enum NextAction
    {
        None,
        FindGame,
        BrowseUnreal,
        OpenDotnet,
        Setup,
        Cook,
        Play,
    }

    public readonly record struct Next(string Title, string Body, NextAction Action, string Cta);
    public readonly record struct Hover(string Title, string Body);
    public readonly record struct FlowStep(string Id, string Label, bool Done, bool Current, bool Visible);

    public static Next NextStep(DetectedTools t)
    {
        if (t.GamePaks is null)
            return new("Find the game", "Scans Steam on every drive, or you pick the folder.", NextAction.FindGame, "Find game");
        if (!t.HasDotnet)
            return new("Install .NET 8", "SDK from Microsoft, then reopen RoweMod.", NextAction.OpenDotnet, "Open download");
        if (!t.HasRetoc)
            return new("Run Setup", "Copies the clothing menus once.", NextAction.Setup, "Run Setup");
        if (CookPending(t) && t.UnrealEditor is null)
            return new("Find Unreal 5.4", "Needed only when a paint or shape is waiting.", NextAction.BrowseUnreal, "Browse Unreal");
        if (CookPending(t) || t.OverlayUtoc is null)
            return new("Play", "Cooks if needed, writes the overlay, and launches.", NextAction.Play, "Play");
        return new("Ready", "Create or Gallery, then Play.", NextAction.None, "");
    }

    public static IReadOnlyList<FlowStep> FlowSteps(DetectedTools t)
    {
        var game = t.GamePaks != null;
        var setup = game && t.HasRetoc;
        var cookNeeded = CookPending(t);
        var cookDone = setup && !cookNeeded;
        var playDone = t.OverlayUtoc != null && !cookNeeded;

        var next = NextStep(t);
        string current = next.Action switch
        {
            NextAction.FindGame => "game",
            NextAction.Setup or NextAction.OpenDotnet => "setup",
            NextAction.BrowseUnreal => "cook",
            NextAction.Cook => "cook",
            NextAction.Play => cookNeeded ? "cook" : "play",
            _ => "",
        };

        return new[]
        {
            new FlowStep("game", "Game", game, current == "game", true),
            new FlowStep("setup", "Setup", setup, current == "setup", true),
            new FlowStep("cook", "Cook", cookDone && setup, current == "cook", cookNeeded || current == "cook"),
            new FlowStep("play", "Play", playDone, current == "play", true),
        };
    }

    public static Hover? ButtonHover(string? id, DetectedTools t)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var setupDone = t.HasRetoc && t.GamePaks != null;
        return id switch
        {
            "setup" when setupDone => new("Setup", "Already done."),
            "setup" => new("Setup", "Copies menus from the game. Once."),
            "create" => new("Create", "Paint or shape clothes and skates."),
            "gallery" => new("Gallery", "Subscribe, then Play."),
            "pack" when t.GamePaks is null => new("Play", "Needs the Steam game."),
            "pack" when !t.HasRetoc => new("Play", "Run Setup first."),
            "pack" => new("Play", "Cooks if needed, writes the overlay, launches."),
            _ => null,
        };
    }

    public static string WorkshopBanner(WorkshopDomain domain, WorkshopTab tab) =>
        (domain, tab) switch
        {
            (WorkshopDomain.Skates, WorkshopTab.Paint) => "Paint wheels or a skate texture.",
            (WorkshopDomain.Skates, _) => "New frame or boot.",
            (_, WorkshopTab.Paint) => "Paint a texture.",
            _ => "New shape on the skeleton.",
        };

    public static string WorkshopEmpty(WorkshopDomain domain, WorkshopTab tab, bool pulled)
    {
        if (tab == WorkshopTab.Paint)
            return pulled ? "No paint files yet." : "Get from game.";
        return pulled ? "" : "Get from game first.";
    }

    public static string GalleryBanner => "Subscribe, then Play.";
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

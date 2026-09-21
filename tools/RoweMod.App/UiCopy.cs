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
            return new("Find Unreal 5.4", "Needed only to Cook a paint or mesh.", NextAction.BrowseUnreal, "Browse Unreal");
        if (CookPending(t))
            return new("Cook", "Unreal prepares the paint or mesh.", NextAction.Cook, "Cook");
        if (t.OverlayUtoc is null)
            return new("Play", "Writes the examples into the game and launches.", NextAction.Play, "Play");
        return new("Ready", "Clothing, Skates, or Gallery — then Play again.", NextAction.None, "");
    }

    public static IReadOnlyList<FlowStep> FlowSteps(DetectedTools t)
    {
        var game = t.GamePaks != null;
        var setup = game && t.HasRetoc;
        var cookNeeded = CookPending(t);
        var cookDone = setup && !cookNeeded;
        var playDone = t.OverlayUtoc != null;

        var next = NextStep(t);
        string current = next.Action switch
        {
            NextAction.FindGame => "game",
            NextAction.Setup or NextAction.OpenDotnet => "setup",
            NextAction.BrowseUnreal or NextAction.Cook => "cook",
            NextAction.Play => "play",
            _ => playDone ? "" : "play",
        };

        return new[]
        {
            new FlowStep("game", "Game", game, current == "game", true),
            new FlowStep("setup", "Setup", setup, current == "setup", true),
            new FlowStep("cook", "Cook", cookDone && setup, current == "cook", cookNeeded || current == "cook"),
            new FlowStep("play", "Play", playDone, current == "play" || (next.Action == NextAction.None && !playDone), true),
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
            "clothing" => new("Clothing", "Paint a PNG or make a new mesh."),
            "skates" => new("Skates", "Boots, frames, wheels."),
            "gallery" => new("Gallery", "Subscribe, then Play. Submit is a PR."),
            "cook" when t.UnrealEditor is null => new("Cook", "Needs Unreal 5.4."),
            "cook" => new("Cook", "Prepares PNG or mesh. Then Play."),
            "pack" when t.GamePaks is null => new("Play", "Needs the Steam game."),
            "pack" when !t.HasRetoc => new("Play", "Run Setup first."),
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

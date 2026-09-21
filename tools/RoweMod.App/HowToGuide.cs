using System.Diagnostics;

namespace RoweMod.App;

sealed class HowToGuide : Panel
{
    readonly Label _nextTitle = new();
    readonly Label _nextBody = new();
    readonly LinkLabel _browse = new();
    readonly Label _hoverTitle = new();
    readonly Label _hoverBody = new();
    readonly List<Label> _wrap = new();
    DetectedTools? _tools;
    string? _pinnedButton;

    public event EventHandler? BrowseGameRequested;

    public HowToGuide()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        BackColor = Color.FromArgb(22, 24, 28);
        Padding = new Padding(16, 12, 12, 16);

        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = Point.Empty,
        };

        stack.Controls.Add(Heading("How to"));
        stack.Controls.Add(Space(4));
        stack.Controls.Add(Card(
            Color.FromArgb(34, 52, 42),
            Eyebrow("DO THIS NEXT", Color.FromArgb(120, 220, 170)),
            Title(_nextTitle),
            Body(_nextBody),
            BrowseLink()));
        stack.Controls.Add(Space(8));
        stack.Controls.Add(Card(
            Color.FromArgb(32, 36, 42),
            Eyebrow("THIS BUTTON", Color.FromArgb(180, 200, 220)),
            Title(_hoverTitle),
            Body(_hoverBody)));
        stack.Controls.Add(Space(10));
        stack.Controls.Add(Eyebrow("THREE WAYS TO ADD CLOTHES", Color.FromArgb(160, 170, 180)));
        stack.Controls.Add(Space(6));
        stack.Controls.Add(PathCard(
            "1  Tint a color",
            Color.FromArgb(255, 220, 100),
            "No extra apps. Clothing → Color tints → Add this color to the game → Pack and Play."));
        stack.Controls.Add(Space(6));
        stack.Controls.Add(PathCard(
            "2  Paint a texture",
            Color.FromArgb(255, 170, 120),
            "Needs Unreal 5.4.4. Clothing → Get clothes from my game → Make a paint mod → edit the PNG → Pack and Play."));
        stack.Controls.Add(Space(6));
        stack.Controls.Add(PathCard(
            "3  Make a new shape",
            Color.FromArgb(140, 190, 255),
            "Needs Blender 5.1 and Unreal 5.4.4. Clothing → Create a garment. Gray bones are the real rig. Model clothes, Export, Cook, Pack and Play."));
        stack.Controls.Add(Space(14));
        stack.Controls.Add(RepoLink());

        Controls.Add(stack);
        Resize += (_, _) => WrapAll();
        ShowOverview();
    }

    public void Bind(DetectedTools tools)
    {
        _tools = tools;
        if (_pinnedButton == null)
            ShowOverview();
        UpdateNext(tools);
        WrapAll();
    }

    public void ShowButton(string? id)
    {
        _pinnedButton = id;
        if (id == null || _tools == null)
        {
            ShowOverview();
            if (_tools != null) UpdateNext(_tools);
            WrapAll();
            return;
        }

        var setupDone = _tools.HasRetoc && _tools.GamePaks != null;
        _hoverTitle.Text = id switch
        {
            "setup" => setupDone ? "Setup (already done)" : "Setup",
            "clothing" => "Clothing",
            "cook" => "Cook",
            "pack" => "Pack and Play",
            "meshrun" => "Mesh to game",
            _ => id,
        };
        _hoverBody.Text = id switch
        {
            "setup" when setupDone =>
                "Finished. The green chip means the clothing menus are on this PC.\n\nYou do not need to click this again.",
            "setup" =>
                "First click after install. Finds Rollout on Steam and copies the clothing menus into this folder.\n\nNo Blender or Unreal needed.",
            "clothing" =>
                "Open this to paint, tint, or make a new shape.\n\nNew mesh opens Blender on the real game skeleton (gray bones, no clothes). Export only after you modeled a garment.",
            "cook" =>
                "Unreal prepares a painted PNG or the last exported mesh. Does not launch the game.\n\nPack and Play cooks for you if a paint file is newer.\n\n" +
                Needs(_tools.UnrealEditor != null, "Unreal 5.4.4 is installed.", "Install Unreal 5.4.4. This button stays off until then."),
            "pack" =>
                "Writes your real items into Rollout and launches.\n\nExample navy/rowe tints stay out unless you added one.\n\nPaint: just this button. New mesh: Export first, then this (or Mesh to game).",
            "meshrun" =>
                "Shortcut after you modeled: export, cook, pack, launch.\n\nDo not use this for a color or a painted PNG.",
            _ => "",
        };
        WrapAll();
    }

    void ShowOverview()
    {
        _hoverTitle.Text = "Hover a button above";
        _hoverBody.Text = "You already installed. Setup once, Pack and Play to see the examples, then Clothing to make your own.";
    }

    void UpdateNext(DetectedTools t)
    {
        _browse.Visible = t.GamePaks is null;
        if (t.GamePaks is null)
        {
            _nextTitle.Text = "Find the game";
            _nextBody.Text = "Install Rollout Inline on Steam, then click Setup. Or browse to RollerSkate → Content → Paks.";
            return;
        }
        if (!t.HasDotnet)
        {
            _nextTitle.Text = "Install .NET 8";
            _nextBody.Text = "The app needs the .NET 8 SDK. Download it from Microsoft, then open RoweMod.cmd again.";
            return;
        }
        if (!t.HasRetoc)
        {
            _nextTitle.Text = "Click Setup";
            _nextBody.Text = "One time. Finds the game and copies the clothing menus. The button greys out when it is done.";
            return;
        }
        if (t.OverlayUtoc is null)
        {
            _nextTitle.Text = "Click Pack and Play";
            _nextBody.Text = "Puts the baggy tee and Rowe jeans in the game, then launches Rollout. That is the first test that install worked.";
            return;
        }
        var wip = ClothingLibrary.Scan(t.Repo).FirstOrDefault(p => p.IsEmptyRig);
        if (wip != null)
        {
            _nextTitle.Text = "Or model " + wip.Title;
            _nextBody.Text = "Optional. Clothing → Open in Blender. Gray bones are the real rig. Add a clothing mesh, then Export this mesh.";
            return;
        }
        _nextTitle.Text = "Open Clothing";
        _nextBody.Text = "Paint a texture, add a color tint, or create a garment. Pack and Play writes only what you made.";
    }

    void WrapAll()
    {
        var w = Math.Max(220, ClientSize.Width - Padding.Horizontal - 12);
        foreach (Control c in Controls)
        {
            if (c is not FlowLayoutPanel stack) continue;
            stack.Width = w;
            foreach (Control child in stack.Controls)
            {
                if (child is Panel card)
                {
                    card.Width = w;
                    foreach (Control inner in card.Controls)
                    {
                        if (inner is FlowLayoutPanel flow)
                            flow.Width = w;
                    }
                }
            }
        }
        foreach (var lab in _wrap)
            lab.MaximumSize = new Size(Math.Max(180, w - 36), 0);
    }

    LinkLabel BrowseLink()
    {
        _browse.Text = "Browse for the game folder";
        _browse.AutoSize = true;
        _browse.LinkColor = Color.FromArgb(120, 210, 255);
        _browse.ActiveLinkColor = Color.White;
        _browse.VisitedLinkColor = Color.FromArgb(120, 210, 255);
        _browse.Margin = new Padding(0, 8, 0, 0);
        _browse.LinkClicked += (_, _) => BrowseGameRequested?.Invoke(this, EventArgs.Empty);
        _browse.Visible = false;
        return _browse;
    }

    static Control RepoLink()
    {
        var link = new LinkLabel
        {
            Text = "github.com/xrowex/RoweMod-Rollout",
            AutoSize = true,
            LinkColor = Color.FromArgb(140, 170, 190),
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Color.FromArgb(140, 170, 190),
            Margin = new Padding(0, 4, 0, 0),
        };
        link.LinkClicked += (_, _) =>
        {
            Process.Start(new ProcessStartInfo("https://github.com/xrowex/RoweMod-Rollout") { UseShellExecute = true });
        };
        return link;
    }

    static string Needs(bool ok, string have, string missing) => ok ? have : missing;

    Label Heading(string text)
    {
        var l = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 18f),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 2),
        };
        _wrap.Add(l);
        return l;
    }

    static Label Eyebrow(string text, Color color) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 8f),
        ForeColor = color,
        Margin = new Padding(0, 0, 0, 4),
    };

    Label Title(Label l)
    {
        l.AutoSize = true;
        l.Font = new Font("Segoe UI Semibold", 13f);
        l.ForeColor = Color.White;
        l.Margin = new Padding(0, 0, 0, 6);
        _wrap.Add(l);
        return l;
    }

    Label Body(Label l)
    {
        l.AutoSize = true;
        l.Font = new Font("Segoe UI", 10f);
        l.ForeColor = Color.FromArgb(210, 216, 222);
        _wrap.Add(l);
        return l;
    }

    Panel PathCard(string title, Color accent, string body)
    {
        var t = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12f),
            ForeColor = accent,
            Margin = new Padding(0, 0, 0, 4),
        };
        var b = new Label
        {
            Text = body,
            AutoSize = true,
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(210, 216, 222),
        };
        _wrap.Add(t);
        _wrap.Add(b);
        return Card(Color.FromArgb(30, 34, 40), t, b);
    }

    static Label Space(int h) => new() { Height = h, Width = 1, Margin = Padding.Empty };

    static Panel Card(Color back, params Control[] children)
    {
        var inner = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 10, 12, 12),
            BackColor = back,
            Location = Point.Empty,
        };
        foreach (var c in children)
            inner.Controls.Add(c);
        var wrap = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = back,
            Margin = new Padding(0, 0, 0, 0),
        };
        wrap.Controls.Add(inner);
        return wrap;
    }
}

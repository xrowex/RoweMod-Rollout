using System.Diagnostics;

namespace RoweMod.App;

sealed class HowToGuide : Panel
{
    readonly Label _nextTitle = new();
    readonly Label _nextBody = new();
    readonly Button _cta = new();
    readonly LinkLabel _secondary = new();
    readonly FlowLayoutPanel _hoverCard;
    readonly Label _hoverTitle = new();
    readonly Label _hoverBody = new();
    readonly List<Label> _wrap = new();
    DetectedTools? _tools;
    UiCopy.NextAction _action = UiCopy.NextAction.None;

    public string NextTitle => _nextTitle.Text;
    public bool HoverShown => _hoverOn;
    public string HoverTitle => _hoverTitle.Text;
    public string CtaText => _cta.Text;
    bool _hoverOn;

    public event EventHandler? NextActionRequested;
    public event EventHandler? BrowseGameRequested;
    public event EventHandler? BrowseUnrealRequested;

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
            Eyebrow("NEXT", Color.FromArgb(120, 220, 170)),
            Title(_nextTitle),
            Body(_nextBody),
            CtaButton(),
            SecondaryLink()));
        stack.Controls.Add(Space(8));
        _hoverCard = Card(
            Color.FromArgb(32, 36, 42),
            Eyebrow("THIS BUTTON", Color.FromArgb(180, 200, 220)),
            Title(_hoverTitle),
            Body(_hoverBody));
        _hoverCard.Visible = false;
        stack.Controls.Add(_hoverCard);
        stack.Controls.Add(Space(14));
        stack.Controls.Add(RepoLink());

        Controls.Add(stack);
        Resize += (_, _) => WrapAll();
    }

    public void Bind(DetectedTools tools)
    {
        _tools = tools;
        var next = UiCopy.NextStep(tools);
        _action = next.Action;
        _nextTitle.Text = next.Title;
        _nextBody.Text = next.Body;

        var hasCta = next.Action != UiCopy.NextAction.None && !string.IsNullOrWhiteSpace(next.Cta);
        _cta.Visible = hasCta;
        _cta.Text = next.Cta;
        _cta.Enabled = true;

        _secondary.Visible = next.Action is UiCopy.NextAction.FindGame or UiCopy.NextAction.BrowseUnreal;
        _secondary.Text = next.Action switch
        {
            UiCopy.NextAction.BrowseUnreal => "Pick UnrealEditor.exe",
            UiCopy.NextAction.FindGame => "Browse folder instead",
            _ => "",
        };
        WrapAll();
    }

    public void ShowButton(string? id)
    {
        if (_tools == null)
        {
            _hoverOn = false;
            _hoverCard.Visible = false;
            WrapAll();
            return;
        }

        var hover = UiCopy.ButtonHover(id, _tools);
        if (hover == null)
        {
            _hoverOn = false;
            _hoverCard.Visible = false;
            WrapAll();
            return;
        }

        _hoverTitle.Text = hover.Value.Title;
        _hoverBody.Text = hover.Value.Body;
        _hoverOn = true;
        _hoverCard.Visible = true;
        WrapAll();
    }

    void WrapAll()
    {
        var w = Math.Max(220, ClientSize.Width - Padding.Horizontal - 12);
        foreach (Control c in Controls)
        {
            if (c is not FlowLayoutPanel stack) continue;
            stack.MaximumSize = new Size(w, 0);
            stack.Width = w;
            foreach (Control child in stack.Controls)
            {
                if (child is FlowLayoutPanel card)
                {
                    card.Width = w;
                    _cta.Width = Math.Max(160, w - 48);
                }
            }
        }
        foreach (var lab in _wrap)
            lab.MaximumSize = new Size(Math.Max(180, w - 36), 0);
    }

    Button CtaButton()
    {
        _cta.Text = "Continue";
        _cta.AutoSize = false;
        _cta.Height = 40;
        _cta.Width = 200;
        _cta.Margin = new Padding(0, 10, 0, 0);
        _cta.FlatStyle = FlatStyle.Flat;
        _cta.Font = new Font("Segoe UI Semibold", 11f);
        _cta.BackColor = Color.FromArgb(56, 140, 96);
        _cta.ForeColor = Color.White;
        _cta.UseVisualStyleBackColor = false;
        _cta.Cursor = Cursors.Hand;
        _cta.FlatAppearance.BorderColor = Color.FromArgb(120, 210, 160);
        _cta.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 165, 115);
        _cta.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 110, 75);
        _cta.Click += (_, _) => NextActionRequested?.Invoke(this, EventArgs.Empty);
        _cta.Visible = false;
        return _cta;
    }

    LinkLabel SecondaryLink()
    {
        _secondary.Text = "Browse folder instead";
        _secondary.AutoSize = true;
        _secondary.LinkColor = Color.FromArgb(120, 210, 255);
        _secondary.ActiveLinkColor = Color.White;
        _secondary.VisitedLinkColor = Color.FromArgb(120, 210, 255);
        _secondary.Margin = new Padding(0, 8, 0, 0);
        _secondary.LinkClicked += (_, _) =>
        {
            if (_action == UiCopy.NextAction.BrowseUnreal)
                BrowseUnrealRequested?.Invoke(this, EventArgs.Empty);
            else
                BrowseGameRequested?.Invoke(this, EventArgs.Empty);
        };
        _secondary.Visible = false;
        return _secondary;
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

    static Label Space(int h) => new() { Height = h, Width = 1, Margin = Padding.Empty };

    static FlowLayoutPanel Card(Color back, params Control[] children)
    {
        var inner = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 10, 12, 12),
            BackColor = back,
            Margin = Padding.Empty,
        };
        foreach (var c in children)
            inner.Controls.Add(c);
        return inner;
    }
}

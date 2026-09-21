using RoweMod.Core;

namespace RoweMod.App;

sealed class GalleryForm : Form
{
    readonly DetectedTools _tools;
    readonly FlowLayoutPanel _list = new();
    readonly Label _hint = new();
    readonly Panel _sharePanel = new();
    readonly ComboBox _submitPick = new();
    readonly TextBox _author = new();
    List<CatalogEntry> _catalog = new();

    public GalleryForm(DetectedTools tools)
    {
        _tools = tools;
        Text = "Gallery";
        Width = 900;
        Height = 720;
        MinimumSize = new Size(740, 540);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10f);

        var banner = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(16, 10, 16, 4),
            Text = UiCopy.GalleryBanner,
        };

        var topBar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 6, 12, 6) };
        var refresh = SmallButton("Refresh", () => _ = LoadCatalogAsync());
        refresh.Left = 0;
        refresh.Top = 2;
        var share = SmallButton("Share…", ToggleShare);
        share.Left = 110;
        share.Top = 2;
        topBar.Controls.Add(refresh);
        topBar.Controls.Add(share);

        _sharePanel.Dock = DockStyle.Top;
        _sharePanel.Height = 0;
        _sharePanel.Visible = false;
        _sharePanel.Padding = new Padding(12, 4, 12, 8);
        _sharePanel.BackColor = Color.FromArgb(24, 28, 32);
        _submitPick.Left = 0;
        _submitPick.Top = 6;
        _submitPick.Width = 280;
        _submitPick.DropDownStyle = ComboBoxStyle.DropDownList;
        _author.Left = 290;
        _author.Top = 8;
        _author.Width = 160;
        _author.PlaceholderText = "Your name";
        var submit = SmallButton("Submit", () => _ = SubmitAsync());
        submit.Left = 460;
        submit.Top = 6;
        submit.AutoSize = true;
        _sharePanel.Controls.Add(_submitPick);
        _sharePanel.Controls.Add(_author);
        _sharePanel.Controls.Add(submit);

        _hint.Dock = DockStyle.Top;
        _hint.Height = 0;
        _hint.Padding = new Padding(16, 4, 16, 4);
        _hint.ForeColor = Color.FromArgb(180, 210, 200);
        _hint.Text = "";
        _hint.Visible = false;

        _list.Dock = DockStyle.Fill;
        _list.AutoScroll = true;
        _list.WrapContents = false;
        _list.FlowDirection = FlowDirection.TopDown;
        _list.Padding = new Padding(12, 4, 8, 12);
        _list.BackColor = Color.FromArgb(18, 18, 20);

        Controls.Add(_list);
        Controls.Add(_hint);
        Controls.Add(_sharePanel);
        Controls.Add(topBar);
        Controls.Add(banner);

        FillSubmitPick();
        Shown += async (_, _) => await LoadCatalogAsync();
    }

    void ToggleShare()
    {
        var on = !_sharePanel.Visible;
        _sharePanel.Visible = on;
        _sharePanel.Height = on ? 48 : 0;
        if (on) FillSubmitPick();
    }

    void FillSubmitPick()
    {
        _submitPick.Items.Clear();
        foreach (var piece in ClothingLibrary.Scan(_tools.Repo)
                     .Where(p => !p.Sample && !p.FromGame && p.ItemJson != null && p.Kind != ClothingKind.Kit))
            _submitPick.Items.Add(new SubmitChoice(piece));
        if (_submitPick.Items.Count > 0)
            _submitPick.SelectedIndex = 0;
    }

    async Task LoadCatalogAsync()
    {
        SetHint("Loading catalog…", ok: true);
        try
        {
            var client = new GalleryClient();
            var file = await client.FetchCatalogAsync();
            _catalog = file.Mods;
            Rebuild();
            SetHint(_catalog.Count == 0
                ? "Catalog is empty."
                : _catalog.Count + " mod(s).", ok: true);
        }
        catch (Exception ex)
        {
            SetHint("Could not load catalog: " + ex.Message, ok: false);
        }
    }

    void Rebuild()
    {
        _list.SuspendLayout();
        _list.Controls.Clear();
        if (_catalog.Count == 0)
        {
            _list.Controls.Add(new Label
            {
                AutoSize = false,
                Height = 48,
                Font = new Font("Segoe UI", 11f),
                ForeColor = Color.FromArgb(160, 176, 186),
                Text = UiCopy.GalleryEmpty,
                Padding = new Padding(4, 12, 4, 4),
            });
        }
        foreach (var entry in _catalog)
            _list.Controls.Add(MakeCard(entry));
        SizeCards();
        _list.ResumeLayout();
    }

    Control MakeCard(CatalogEntry entry)
    {
        var on = SubscriptionStore.IsSubscribed(entry.Id);
        var card = new Panel
        {
            Height = 96,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.FromArgb(32, 36, 42),
        };
        var title = new Label
        {
            Left = 12,
            Top = 10,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12f),
            Text = entry.Title,
        };
        var sub = new Label
        {
            Left = 12,
            Top = 34,
            AutoSize = true,
            ForeColor = Color.FromArgb(180, 200, 210),
            Text = entry.Slot + "  ·  " + entry.Author + "  ·  " + entry.Row,
        };
        var buttons = new FlowLayoutPanel
        {
            Left = 12,
            Top = 56,
            Height = 32,
            Width = 700,
            WrapContents = false,
        };
        var toggle = SmallButton(on ? "Unsubscribe" : "Subscribe", () => _ = ToggleAsync(entry, !on));
        if (!on) toggle.BackColor = Color.FromArgb(50, 110, 80);
        buttons.Controls.Add(toggle);
        card.Controls.Add(title);
        card.Controls.Add(sub);
        card.Controls.Add(buttons);
        return card;
    }

    async Task ToggleAsync(CatalogEntry entry, bool on)
    {
        try
        {
            if (on)
            {
                SetHint("Downloading " + entry.Title + "…", ok: true);
                var client = new GalleryClient();
                await client.DownloadModAsync(_tools.Repo, entry, line => { });
            }
            else
            {
                var dir = AppPaths.WorkshopMod(_tools.Repo, entry.Id);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            SubscriptionStore.Set(entry.Id, on);
            Rebuild();
            SetHint(on ? "Subscribed to " + entry.Title + "." : "Removed " + entry.Title + ".", ok: true);
        }
        catch (Exception ex)
        {
            SetHint(ex.Message, ok: false);
        }
    }

    async Task SubmitAsync()
    {
        if (_submitPick.SelectedItem is not SubmitChoice choice || choice.Piece.ItemJson == null)
        {
            MessageBox.Show(this, "Pick a live item you already cooked.", "Gallery", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var token = GitHubAuth.ResolveToken();
            var clientId = Environment.GetEnvironmentVariable("ROWE_GITHUB_CLIENT_ID");
            if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(clientId))
            {
                SetHint("Finish GitHub login in the browser…", ok: true);
                token = await GitHubAuth.DeviceFlowAsync(clientId, msg => SetHint(msg, ok: true));
            }
            if (string.IsNullOrWhiteSpace(token))
                token = AskToken();
            if (string.IsNullOrWhiteSpace(token))
                return;

            var dest = Path.Combine(_tools.Repo, "dumps", "submit", choice.Piece.Id);
            SetHint("Packaging " + choice.Piece.Title + "…", ok: true);
            await Task.Run(() => ModPackage.Build(_tools.Repo, choice.Piece.ItemJson, _author.Text, dest));
            SetHint("Opening pull request…", ok: true);
            var client = new GalleryClient(token);
            var url = await client.SubmitPrAsync(dest, "Add " + choice.Piece.Title, line => { });
            SetHint("PR opened. " + url, ok: true);
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            SetHint(ex.Message, ok: false);
            MessageBox.Show(this, ex.Message, "Submit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    string? AskToken()
    {
        using var dlg = new Form
        {
            Text = "GitHub login",
            Width = 520,
            Height = 220,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(28, 28, 32),
            ForeColor = Color.WhiteSmoke,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };
        var lab = new Label
        {
            Left = 16,
            Top = 16,
            Width = 470,
            Height = 56,
            Text = "Paste a GitHub token with public_repo (or sign in with gh auth login). Device flow needs an OAuth app client id.",
        };
        var box = new TextBox { Left = 16, Top = 80, Width = 470 };
        var ok = new Button { Text = "Save token", Left = 300, Top = 120, Width = 90, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 400, Top = 120, Width = 86, DialogResult = DialogResult.Cancel };
        dlg.Controls.AddRange(new Control[] { lab, box, ok, cancel });
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;
        if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(box.Text))
            return null;
        var token = box.Text.Trim();
        GitHubAuth.SaveToken(token);
        return token;
    }

    void SetHint(string text, bool ok)
    {
        _hint.ForeColor = ok ? Color.FromArgb(180, 210, 200) : Color.FromArgb(255, 180, 120);
        _hint.Text = text;
        var on = !string.IsNullOrWhiteSpace(text);
        _hint.Visible = on;
        _hint.Height = on ? 36 : 0;
    }

    static Button SmallButton(string text, Action click)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 70, 90),
            ForeColor = Color.White,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 120, 140);
        b.Click += (_, _) => click();
        return b;
    }

    void SizeCards()
    {
        var w = Math.Max(400, _list.ClientSize.Width - 28);
        foreach (Control c in _list.Controls)
            c.Width = w;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        SizeCards();
    }

    sealed class SubmitChoice
    {
        public ClothingPiece Piece { get; }
        public SubmitChoice(ClothingPiece piece) => Piece = piece;
        public override string ToString() => Piece.Title;
    }
}

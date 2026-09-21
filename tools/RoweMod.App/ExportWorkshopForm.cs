namespace RoweMod.App;

enum WorkshopDomain
{
    Clothing,
    Skates,
}

enum WorkshopTab
{
    Paint,
    Mesh,
}

sealed class ExportWorkshopForm : Form
{
    readonly DetectedTools _tools;
    readonly WorkshopDomain _domain;
    List<ClothingPiece> _pieces;
    readonly Label _banner = new();
    readonly Label _hint = new();
    readonly FlowLayoutPanel _list = new();
    readonly List<Panel> _modeCards = new();
    WorkshopTab? _tab;

    public bool ExportMesh { get; private set; }
    public ClothingPiece? ExportTarget { get; private set; }
    public bool SavedCookTarget { get; private set; }
    public string BannerText => _banner.Text;

    public void ShowTab(WorkshopTab tab) => SetTab(tab);

    IEnumerable<ClothingPiece> VisiblePieces => _pieces.Where(Matches);

    bool Matches(ClothingPiece p)
    {
        if (p.Kind == ClothingKind.Kit || p.Sample) return false;
        if (_domain == WorkshopDomain.Skates) return ClothingLibrary.IsSkate(p);
        return ClothingLibrary.IsClothingSlot(p.Slot);
    }

    public ExportWorkshopForm(DetectedTools tools, WorkshopDomain domain = WorkshopDomain.Clothing)
    {
        _tools = tools;
        _domain = domain;
        _pieces = ClothingLibrary.Scan(tools.Repo).ToList();
        var skate = domain == WorkshopDomain.Skates;

        Text = skate ? "Skates" : "Clothing";
        Width = 920;
        Height = 740;
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10f);

        _banner.Dock = DockStyle.Top;
        _banner.Height = 36;
        _banner.Padding = new Padding(16, 10, 16, 4);
        _banner.Text = UiCopy.WorkshopBanner(domain, WorkshopTab.Paint);

        var pick = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 72,
            ColumnCount = 2,
            Padding = new Padding(12, 0, 12, 8),
        };
        pick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var paint = ModeCard("Paint", "Edit a texture.");
        var mesh = ModeCard("New mesh", skate ? "Copy a game frame or boot." : "Model on the skeleton.");
        WireClicks(paint, () => SetTab(WorkshopTab.Paint));
        WireClicks(mesh, () => SetTab(WorkshopTab.Mesh));
        _modeCards.AddRange(new[] { paint, mesh });
        pick.Controls.Add(paint, 0, 0);
        pick.Controls.Add(mesh, 1, 0);
        foreach (Control c in pick.Controls) c.Dock = DockStyle.Fill;

        var pullBar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 0, 12, 8) };
        var pull = new Button
        {
            Text = _tools.HasPulledClothing ? "Refresh from game" : "Get from game",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 90, 50),
            ForeColor = Color.White,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
        };
        pull.FlatAppearance.BorderColor = Color.FromArgb(140, 180, 90);
        pull.Click += async (_, _) => await PullFromGame(pull);
        pullBar.Controls.Add(pull);

        _hint.Dock = DockStyle.Top;
        _hint.Height = 0;
        _hint.Padding = new Padding(16, 2, 16, 2);
        _hint.ForeColor = Color.FromArgb(180, 210, 200);
        _hint.Text = "";
        _hint.Visible = false;

        _list.Dock = DockStyle.Fill;
        _list.AutoScroll = true;
        _list.WrapContents = false;
        _list.FlowDirection = FlowDirection.TopDown;
        _list.Padding = new Padding(12, 8, 8, 16);
        _list.BackColor = Color.FromArgb(18, 18, 20);

        Controls.Add(_list);
        Controls.Add(_hint);
        Controls.Add(pullBar);
        Controls.Add(pick);
        Controls.Add(_banner);

        SetTab(WorkshopTab.Paint);
    }

    static void WireClicks(Control root, Action click)
    {
        root.Click += (_, _) => click();
        foreach (Control child in root.Controls)
            child.Click += (_, _) => click();
    }

    Panel ModeCard(string title, string sub)
    {
        var card = new Panel
        {
            Margin = new Padding(6, 4, 6, 4),
            BackColor = Color.FromArgb(45, 70, 90),
            Cursor = Cursors.Hand,
            Padding = new Padding(12, 10, 12, 10),
        };
        var t = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI Semibold", 12f),
            ForeColor = Color.White,
            Text = title,
            Cursor = Cursors.Hand,
        };
        var s = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(200, 214, 222),
            Text = sub,
            Cursor = Cursors.Hand,
        };
        card.Controls.Add(s);
        card.Controls.Add(t);
        return card;
    }

    void StyleModes()
    {
        for (var i = 0; i < _modeCards.Count; i++)
        {
            var on = _tab != null && (int)_tab == i;
            _modeCards[i].BackColor = on ? Color.FromArgb(60, 100, 80) : Color.FromArgb(45, 70, 90);
        }
    }

    async Task PullFromGame(Button pull)
    {
        if (_tools.GamePaks is null)
        {
            MessageBox.Show(this, "Find the game first (Setup or Browse on the main window).", "RoweMod",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        pull.Enabled = false;
        SetHint("Pulling…", ok: true);
        try
        {
            var extra = new[] { "-GamePaks", "\"" + _tools.GamePaks + "\"" };
            var psi = new System.Diagnostics.ProcessStartInfo(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -File \"" + _tools.PullScript + "\" " + string.Join(" ", extra))
            {
                WorkingDirectory = _tools.Repo,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("could not start pull");
            var output = await p.StandardOutput.ReadToEndAsync();
            var err = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            var logPath = Path.Combine(_tools.Repo, "dumps", "game-clothing", "pull.log");
            try { File.WriteAllText(logPath, output + Environment.NewLine + err); } catch { /* ignore */ }
            _pieces = ClothingLibrary.Scan(_tools.Repo).ToList();
            RebuildList();
            var n = VisiblePieces.Count(x => x.FromGame);
            if (p.ExitCode == 0 && n > 0)
            {
                pull.Text = "Refresh from game";
                SetHint("Got " + n + " files.", ok: true);
            }
            else
            {
                SetHint("Could not pull. Is the game installed?", ok: false);
            }
        }
        catch (Exception ex)
        {
            SetHint("Pull failed: " + ex.Message, ok: false);
        }
        finally
        {
            pull.Enabled = true;
        }
    }

    void SetHint(string text, bool ok)
    {
        _hint.ForeColor = ok ? Color.FromArgb(180, 210, 200) : Color.FromArgb(255, 180, 120);
        _hint.Text = text;
        var on = !string.IsNullOrWhiteSpace(text);
        _hint.Visible = on;
        _hint.Height = on ? 28 : 0;
    }

    void SetTab(WorkshopTab tab)
    {
        _tab = tab;
        StyleModes();
        _banner.Text = UiCopy.WorkshopBanner(_domain, tab);
        RebuildList();
    }

    void RebuildList()
    {
        _list.SuspendLayout();
        _list.Controls.Clear();
        if (_tab == null) { _list.ResumeLayout(); return; }

        if (_tab == WorkshopTab.Mesh)
            _list.Controls.Add(StartMeshCard());

        var groups = Groups().ToList();
        var any = groups.Any(g => g.Items.Count > 0);
        if (!any)
        {
            var empty = UiCopy.WorkshopEmpty(_domain, _tab.Value, _tools.HasPulledClothing);
            if (!string.IsNullOrEmpty(empty))
                _list.Controls.Add(EmptyState(empty));
        }

        foreach (var (title, items) in groups)
        {
            if (items.Count == 0) continue;
            _list.Controls.Add(Section(title));
            foreach (var piece in items)
                _list.Controls.Add(MakeCard(piece));
        }

        SizeCards();
        _list.ResumeLayout();
    }

    IEnumerable<(string Title, List<ClothingPiece> Items)> Groups()
    {
        if (_tab == WorkshopTab.Paint)
        {
            yield return ("Yours", VisiblePieces.Where(p => !p.FromGame && TextureMods.IsPaintTarget(p)).ToList());
            var pulled = VisiblePieces.Where(p => p.FromGame && TextureMods.IsPaintTarget(p)).ToList();
            foreach (var g in pulled
                         .GroupBy(p => p.Garment ?? p.Slot)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                yield return (g.Key, g.OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase).ToList());
        }
        else
        {
            yield return ("Yours",
                VisiblePieces.Where(p => !p.FromGame && p.Kind == ClothingKind.Mesh).ToList());
            var game = VisiblePieces.Where(p => p.Id.StartsWith("game:", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var g in game
                         .GroupBy(p => p.Slot)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                yield return (g.Key, g.OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase).ToList());
        }
    }

    Control StartMeshCard()
    {
        var card = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(14, 12, 14, 14),
            BackColor = Color.FromArgb(36, 56, 44),
        };
        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 13f),
            Margin = new Padding(0, 0, 0, 4),
            Text = _domain == WorkshopDomain.Skates ? "New frame or boot" : "New garment",
        };
        var sub = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(180, 210, 190),
            Margin = new Padding(0, 0, 0, 10),
            Text = _domain == WorkshopDomain.Skates ? "Copies a pulled mesh." : "Opens the skeleton in Blender.",
        };
        var go = SmallButton("Create", StartNewMesh, primary: true);
        go.Margin = new Padding(0, 0, 0, 0);
        card.Controls.Add(title);
        card.Controls.Add(sub);
        card.Controls.Add(go);
        return card;
    }

    void StartNewMesh()
    {
        using var dlg = new NewMeshForm(_domain);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(dlg.GarmentName))
        {
            MessageBox.Show(this, _domain == WorkshopDomain.Skates ? "Give the part a name." : "Give the garment a name.", "RoweMod", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var piece = _domain == WorkshopDomain.Skates
                ? MeshWork.CreateSkate(_tools.Repo, dlg.GarmentName, dlg.Slot)
                : MeshWork.CreateMesh(_tools.Repo, dlg.GarmentName, dlg.Slot);
            var open = piece.Blend ?? piece.Model;
            if (_tools.Blender != null && open != null)
                ClothingLibrary.OpenInBlender(_tools.Blender, open);
            _pieces = ClothingLibrary.Scan(_tools.Repo).ToList();
            SetHint(_domain == WorkshopDomain.Skates
                ? "Edit the copy, save the glb, Use for Cook."
                : "Gray bones are the rig. Model, then Export.", ok: true);
            RebuildList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "RoweMod", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    static Label Section(string text) => new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9f),
        ForeColor = Color.FromArgb(140, 160, 170),
        Margin = new Padding(0, 10, 0, 4),
        Text = text,
    };

    Control MakeCard(ClothingPiece piece)
    {
        var card = new Panel
        {
            Height = 132,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = piece.Sample ? Color.FromArgb(38, 36, 32) : Color.FromArgb(32, 36, 42),
            Padding = new Padding(10),
        };

        var x = 10;
        if (piece.Preview != null && File.Exists(piece.Preview))
        {
            var pic = new PictureBox
            {
                Left = 10,
                Top = 12,
                Width = 64,
                Height = 64,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(18, 18, 20),
            };
            try { pic.Image = Image.FromFile(piece.Preview); } catch { /* skip bad png */ }
            card.Controls.Add(pic);
            x = 84;
        }

        var title = new Label
        {
            Left = x,
            Top = 6,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12f),
            Text = piece.Title,
        };
        var sub = WrapLabel(HelpText(piece), Color.FromArgb(186, 202, 210));
        sub.Left = x;
        sub.Top = 30;
        sub.Height = 40;
        card.Controls.Add(title);
        card.Controls.Add(sub);

        var buttons = new FlowLayoutPanel
        {
            Left = x,
            Top = 74,
            Height = 48,
            Width = 700,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        foreach (var b in Actions(piece))
            buttons.Controls.Add(b);
        card.Controls.Add(buttons);
        card.Tag = buttons;
        return card;
    }

    IEnumerable<Control> Actions(ClothingPiece piece)
    {
        if (_tab == WorkshopTab.Mesh && piece.Kind == ClothingKind.Mesh && !piece.FromGame && !piece.Sample)
        {
            if (_domain == WorkshopDomain.Skates)
            {
                var cook = SmallButton("Use for Cook", () => QueueSkateCook(piece), primary: true);
                cook.Enabled = piece.Model != null && File.Exists(piece.Model);
                if (!cook.Enabled)
                    _tips.SetToolTip(cook, "Need the glb in art/skates.");
                yield return cook;
            }
            else
            {
                var bind = MeshWork.FindBindGlb(_tools.Repo);
                var exp = SmallButton("Export this mesh", () => BeginExport(piece, bind), primary: true);
                exp.Enabled = piece.Blend != null && bind != null && _tools.Blender != null && !piece.IsEmptyRig;
                if (!exp.Enabled)
                {
                    _tips.SetToolTip(exp, piece.IsEmptyRig
                        ? "Add a clothing mesh in Blender first."
                        : bind == null
                            ? "Get from game first."
                            : "Install Blender 5.1.");
                }
                yield return exp;
            }
        }

        if (piece.Id.StartsWith("tex:", StringComparison.OrdinalIgnoreCase) && piece.PaintFile != null)
        {
            yield return SmallButton("Make a paint mod", () =>
            {
                var made = TextureMods.Promote(_tools.Repo, piece);
                if (made == null)
                {
                    MessageBox.Show(this, "Could not tell which catalog item this texture belongs to.", "RoweMod",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ClothingLibrary.OpenFile(made.Value.Png);
                _pieces = ClothingLibrary.Scan(_tools.Repo).ToList();
                SetHint("Clean fabric normal. Edit PNG, Cook, Play.", ok: true);
                RebuildList();
            }, primary: true);
        }

        if (piece.PaintFile != null && File.Exists(piece.PaintFile) && piece.ItemJson != null && !piece.FromGame)
            yield return SmallButton("Edit PNG", () => ClothingLibrary.OpenFile(piece.PaintFile), primary: _tab == WorkshopTab.Paint);

        var blenderFile = piece.Blend ?? piece.Model;
        if (blenderFile != null)
        {
            var open = SmallButton(piece.FromGame ? "View in Blender" : "Open in Blender", () =>
            {
                if (_tools.Blender is null)
                {
                    MessageBox.Show(this, "Install Blender 5.1 to open this.", "RoweMod", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    ClothingLibrary.OpenInBlender(_tools.Blender, blenderFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Blender", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
            open.Enabled = _tools.Blender != null;
            yield return open;
        }

        if (!piece.Sample && piece.ItemJson != null && piece.Kind == ClothingKind.Texture && !piece.FromGame)
        {
            yield return SmallButton("Remove from game", () =>
            {
                MeshWork.SetSample(piece.ItemJson, true);
                _pieces = ClothingLibrary.Scan(_tools.Repo).ToList();
                RebuildList();
            });
        }
    }

    void QueueSkateCook(ClothingPiece piece)
    {
        if (piece.Model == null || !File.Exists(piece.Model))
        {
            MessageBox.Show(this, "Missing the glb in art/skates. Get skate models, then create the part again.", "RoweMod",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        MeshWork.SaveTarget(_tools.Repo, MeshWork.FromPiece(_tools.Repo, piece));
        SavedCookTarget = true;
        SetHint("Click Cook, then Play.", ok: true);
    }

    void BeginExport(ClothingPiece piece, string? bind)
    {
        if (piece.IsEmptyRig)
        {
            MessageBox.Show(this,
                "This Blender file is still only the skeleton. Add a clothing mesh, weight-paint it, save, then Export.",
                "RoweMod", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (piece.Blend == null)
        {
            MessageBox.Show(this, "This item has no Blender file yet. Use Create a garment.", "RoweMod",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (bind == null)
        {
            MessageBox.Show(this, "Get clothes from my game first. Export needs the hoodie bind pose.", "RoweMod",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ExportTarget = piece;
        ExportMesh = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    readonly ToolTip _tips = new();

    string HelpText(ClothingPiece piece)
    {
        if (piece.IsEmptyRig)
            return "Skeleton only.";
        return piece.Garment ?? piece.Slot;
    }

    static Label EmptyState(string text) => new()
    {
        AutoSize = false,
        Height = 48,
        Font = new Font("Segoe UI", 11f),
        ForeColor = Color.FromArgb(160, 176, 186),
        Text = text,
        Padding = new Padding(4, 12, 4, 4),
    };

    Label WrapLabel(string text, Color color) => new()
    {
        AutoSize = false,
        Width = 620,
        Height = 36,
        Font = new Font("Segoe UI", 9.5f),
        ForeColor = color,
        Text = text,
    };

    static Button SmallButton(string text, Action click, bool primary = false)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(50, 110, 80) : Color.FromArgb(45, 70, 90),
            ForeColor = Color.White,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 4),
            Padding = new Padding(8, 2, 8, 2),
        };
        b.FlatAppearance.BorderColor = primary ? Color.FromArgb(90, 160, 110) : Color.FromArgb(80, 120, 140);
        b.Click += (_, _) => click();
        return b;
    }

    void SizeCards()
    {
        var w = Math.Max(420, _list.ClientSize.Width - 28);
        foreach (Control c in _list.Controls)
        {
            if (c.AutoSize)
            {
                c.MinimumSize = new Size(w, 0);
                c.MaximumSize = new Size(w, 0);
            }
            else
            {
                c.Width = w;
            }
            if (c.Tag is FlowLayoutPanel buttons)
            {
                var left = buttons.Left;
                buttons.Width = Math.Max(200, w - left - 16);
            }
            foreach (Control inner in c.Controls)
            {
                if (inner is Label lab && !lab.AutoSize && lab.Font.Size < 12f)
                    lab.Width = Math.Max(180, w - lab.Left - 16);
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        SizeCards();
        _banner.MaximumSize = new Size(Math.Max(400, ClientSize.Width - 24), 0);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        foreach (Control card in _list.Controls)
        {
            foreach (Control c in card.Controls)
            {
                if (c is PictureBox pic)
                {
                    pic.Image?.Dispose();
                    pic.Image = null;
                }
            }
        }
        base.OnFormClosed(e);
    }
}

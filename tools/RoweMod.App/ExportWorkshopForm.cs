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
    DetectedTools _tools;
    WorkshopDomain _domain;
    List<ClothingPiece> _pieces;
    readonly Label _banner = new();
    readonly Label _hint = new();
    readonly FlowLayoutPanel _list = new();
    readonly List<Panel> _domainCards = new();
    readonly List<Panel> _modeCards = new();
    readonly Panel _playBar = new();
    readonly Button _playCta = new();
    readonly Button _deleted = new();
    bool _showDeleted;
    WorkshopTab? _tab;

    public bool ExportMesh { get; private set; }
    public ClothingPiece? ExportTarget { get; private set; }
    public bool SavedCookTarget { get; private set; }
    public bool LaunchPlay { get; private set; }
    public string BannerText => _banner.Text;
    public WorkshopDomain Domain => _domain;
    public string DomainLabel => _domain == WorkshopDomain.Skates ? "Skates" : "Clothes";
    public string TabLabel => _tab == WorkshopTab.Mesh ? "Shape" : "Paint";

    public void ShowTab(WorkshopTab tab) => SetTab(tab);
    public void ShowDomain(WorkshopDomain domain) => SetDomain(domain);

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

        Text = "Create";
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

        var domainPick = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            ColumnCount = 2,
            Padding = new Padding(12, 4, 12, 4),
        };
        domainPick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        domainPick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var clothes = SegmentCard("Clothes", "Tops, bottoms, and more.");
        var skates = SegmentCard("Skates", "Boots, frames, wheels.");
        WireClicks(clothes, () => SetDomain(WorkshopDomain.Clothing));
        WireClicks(skates, () => SetDomain(WorkshopDomain.Skates));
        _domainCards.AddRange(new[] { clothes, skates });
        domainPick.Controls.Add(clothes, 0, 0);
        domainPick.Controls.Add(skates, 1, 0);
        foreach (Control c in domainPick.Controls) c.Dock = DockStyle.Fill;

        var pick = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            ColumnCount = 2,
            Padding = new Padding(12, 0, 12, 8),
        };
        pick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var paint = SegmentCard("Paint", "Edit a texture.");
        var shape = SegmentCard("Shape", "New mesh.");
        WireClicks(paint, () => SetTab(WorkshopTab.Paint));
        WireClicks(shape, () => SetTab(WorkshopTab.Mesh));
        _modeCards.AddRange(new[] { paint, shape });
        pick.Controls.Add(paint, 0, 0);
        pick.Controls.Add(shape, 1, 0);
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

        _playBar.Dock = DockStyle.Bottom;
        _playBar.Height = 0;
        _playBar.Padding = new Padding(12, 8, 12, 10);
        _playBar.BackColor = Color.FromArgb(28, 40, 34);
        _playBar.Visible = false;
        _playCta.Text = "Play";
        _playCta.Dock = DockStyle.Fill;
        _playCta.FlatStyle = FlatStyle.Flat;
        _playCta.Font = new Font("Segoe UI Semibold", 11f);
        _playCta.BackColor = Color.FromArgb(56, 140, 96);
        _playCta.ForeColor = Color.White;
        _playCta.UseVisualStyleBackColor = false;
        _playCta.Cursor = Cursors.Hand;
        _playCta.FlatAppearance.BorderColor = Color.FromArgb(120, 210, 160);
        _playCta.Click += (_, _) =>
        {
            LaunchPlay = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        _playBar.Controls.Add(_playCta);

        _list.Dock = DockStyle.Fill;
        _list.AutoScroll = true;
        _list.WrapContents = false;
        _list.FlowDirection = FlowDirection.TopDown;
        _list.Padding = new Padding(12, 8, 8, 16);
        _list.BackColor = Color.FromArgb(18, 18, 20);

        Controls.Add(_list);
        Controls.Add(_hint);
        var manage = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(12, 2, 12, 2) };
        manage.Controls.Add(SmallButton("Refresh saved files", Reload));
        _deleted.Text = "Deleted mods";
        _deleted.AutoSize = true;
        _deleted.FlatStyle = FlatStyle.Flat;
        _deleted.Padding = new Padding(8, 2, 8, 2);
        _deleted.Click += (_, _) => { _showDeleted = !_showDeleted; RebuildList(); };
        manage.Controls.Add(_deleted);
        Controls.Add(manage);
        Controls.Add(pullBar);
        Controls.Add(pick);
        Controls.Add(domainPick);
        Controls.Add(_banner);
        Controls.Add(_playBar);

        StyleDomains();
        SetTab(WorkshopTab.Paint);
        Activated += (_, _) => Reload();
    }

    static void WireClicks(Control root, Action click)
    {
        root.Click += (_, _) => click();
        foreach (Control child in root.Controls)
            child.Click += (_, _) => click();
    }

    Panel SegmentCard(string title, string sub)
    {
        var card = new Panel
        {
            Margin = new Padding(4, 2, 4, 2),
            BackColor = Color.FromArgb(45, 70, 90),
            Cursor = Cursors.Hand,
            Padding = new Padding(12, 8, 12, 8),
        };
        var t = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI Semibold", 11f),
            ForeColor = Color.White,
            Text = title,
            Cursor = Cursors.Hand,
        };
        var s = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(200, 214, 222),
            Text = sub,
            Cursor = Cursors.Hand,
        };
        card.Controls.Add(s);
        card.Controls.Add(t);
        return card;
    }

    void SetDomain(WorkshopDomain domain)
    {
        _showDeleted = false;
        _domain = domain;
        StyleDomains();
        _banner.Text = UiCopy.WorkshopBanner(_domain, _tab ?? WorkshopTab.Paint);
        RebuildList();
    }

    void StyleDomains()
    {
        for (var i = 0; i < _domainCards.Count; i++)
        {
            var on = (int)_domain == i;
            _domainCards[i].BackColor = on ? Color.FromArgb(60, 100, 80) : Color.FromArgb(45, 70, 90);
        }
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
            MessageBox.Show(this, "Find the game first (Setup on the main window).", "RoweMod",
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
            _tools = ToolPaths.Detect();
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
        _hint.Height = on ? 48 : 0;
    }

    void ShowPlayBar(bool on)
    {
        _playBar.Visible = on;
        _playBar.Height = on ? 56 : 0;
    }

    void SetTab(WorkshopTab tab)
    {
        _showDeleted = false;
        _tab = tab;
        StyleModes();
        _banner.Text = UiCopy.WorkshopBanner(_domain, tab);
        RebuildList();
    }

    void RebuildList()
    {
        _list.SuspendLayout();
        foreach (Control old in _list.Controls.Cast<Control>().ToArray())
        {
            foreach (var pic in old.Controls.OfType<PictureBox>()) pic.Image?.Dispose();
            old.Dispose();
        }
        var deleted = LocalModTrash.List(_tools.Repo);
        _deleted.Text = _showDeleted ? "Back to my mods" : "Deleted mods (" + deleted.Count + ")";
        if (_showDeleted)
        {
            _list.Controls.Add(EmptyState("Source artwork is kept. Restore a mod, then Play to put it back in-game."));
            foreach (var entry in deleted)
            {
                var row = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 8) };
                row.Controls.Add(new Label { Text = entry.Title, AutoSize = true, Margin = new Padding(0, 8, 20, 0) });
                row.Controls.Add(SmallButton("Restore", () =>
                {
                    try { LocalModTrash.Restore(_tools.Repo, entry); Reload(); SetHint("Restored " + entry.Title + ". Play applies it to the game.", true); ShowPlayBar(true); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Restore mod"); }
                }, true));
                _list.Controls.Add(row);
            }
            SizeCards();
            _list.ResumeLayout();
            return;
        }
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

    void Reload()
    {
        _pieces = ClothingLibrary.Scan(_tools.Repo).ToList();
        RebuildList();
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
            Text = _domain == WorkshopDomain.Skates
                ? "Create a Blender project from a frame or boot. Edit → Ctrl+S → Export + Play."
                : "Create a Blender project with the rig. Model and weight your garment → Ctrl+S → Export + Play.",
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
        if (_tools.Blender == null)
        {
            MessageBox.Show(this, "Install Blender first, then reopen Create.", "New mesh");
            return;
        }
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
            OpenProject(piece);
            _pieces = ClothingLibrary.Scan(_tools.Repo).ToList();
            SetHint(_domain == WorkshopDomain.Skates
                ? "Your project is saved in art/skates. Edit it in Blender, Ctrl+S, then Refresh saved files → Export + Play."
                : "Your project is saved in art/rig. Model and weight the garment, Ctrl+S, then Export + Play.", ok: true);
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
            Height = 174,
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
            try { using var source = Image.FromFile(piece.Preview); pic.Image = new Bitmap(source); } catch { /* skip bad png */ }
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

        if (!piece.FromGame && piece.ItemJson != null)
        {
            var source = _tab == WorkshopTab.Mesh && piece.Kind == ClothingKind.Mesh ? MeshWork.EditableSource(_tools.Repo, piece) : piece.PaintFile;
            var path = new Label { Left = x, Top = 69, Height = 22, Width = 620, AutoEllipsis = true,
                ForeColor = Color.FromArgb(150, 174, 180), Text = source == null ? "" : "Save file: " + Path.GetRelativePath(_tools.Repo, source) };
            _tips.SetToolTip(path, source);
            card.Controls.Add(path);
        }

        var buttons = new FlowLayoutPanel
        {
            Left = x,
            Top = 98,
            Height = 68,
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
            var bind = MeshWork.FindBindGlb(_tools.Repo);
            var exp = SmallButton("Export + Play", () => BeginExport(piece, bind, launchPlay: true), primary: true);
            exp.Enabled = _tools.Blender != null && File.Exists(MeshWork.EditableSource(_tools.Repo, piece));
            _tips.SetToolTip(exp, exp.Enabled ? "Uses the saved file: Ctrl+S for .blend; re-export FBX after editing."
                : "Open in Blender to create the project, then return here after saving.");
            yield return exp;
            yield return SmallButton("Choose mesh…", () => ChooseMeshSource(piece));
        }

        if (piece.Id.StartsWith("tex:", StringComparison.OrdinalIgnoreCase) && piece.PaintFile != null)
        {
            yield return SmallButton("Make a paint mod", () =>
            {
                try
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
                    SetHint("Edit the PNG, then Play.", ok: true);
                    ShowPlayBar(true);
                    RebuildList();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Paint mod", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            }, primary: true);
        }

        if (_tab == WorkshopTab.Paint && piece.PaintFile != null && File.Exists(piece.PaintFile) && piece.ItemJson != null && !piece.FromGame)
        {
            yield return SmallButton("Edit PNG", () => ClothingLibrary.OpenFile(piece.PaintFile), primary: _tab == WorkshopTab.Paint);
            yield return SmallButton("Play", () =>
            {
                LaunchPlay = true;
                DialogResult = DialogResult.OK;
                Close();
            }, primary: true);
        }

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
                    if (piece.FromGame) ClothingLibrary.OpenInBlender(_tools.Blender, blenderFile);
                    else OpenProject(piece);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Blender", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }, primary: !piece.FromGame && !File.Exists(MeshWork.EditableSource(_tools.Repo, piece)));
            open.Enabled = _tools.Blender != null;
            yield return open;
        }

        if (!piece.Sample && piece.ItemJson != null && !piece.FromGame)
        {
            yield return SmallButton("Show files", () =>
            {
                var file = _tab == WorkshopTab.Mesh && piece.Kind == ClothingKind.Mesh ? MeshWork.EditableSource(_tools.Repo, piece) : piece.PaintFile;
                ClothingLibrary.OpenFolder(file != null && File.Exists(file) ? file : piece.Model ?? piece.ItemJson);
            });
            yield return SmallButton("Delete…", () => DeleteMod(piece));
        }
    }

    void OpenProject(ClothingPiece piece)
    {
        if (_tools.Blender == null) throw new InvalidOperationException("Install Blender, then reopen Create.");
        var blend = MeshWork.EditableSource(_tools.Repo, piece);
        if (File.Exists(blend)) ClothingLibrary.OpenInBlender(_tools.Blender, blend);
        else if (piece.Blend == null && piece.Model != null && ClothingLibrary.IsSkate(piece))
        {
            var frames = piece.Slot.Equals("Frames", StringComparison.OrdinalIgnoreCase);
            var reference = MeshWork.FindSkateRef(_tools.Repo, frames ? "Boots" : "Frames")
                ?? throw new InvalidOperationException("Get skate models from the game first so both boots and frames are available for reference.");
            ClothingLibrary.OpenInBlender(_tools.Blender, piece.Model, blend, reference, frames ? "frames" : "boots");
        }
        else throw new InvalidOperationException("Choose your saved .blend or .fbx file first.");
    }

    void ChooseMeshSource(ClothingPiece piece)
    {
        using var picker = new OpenFileDialog { Title = "Choose the .blend or FBX saved for " + piece.Title,
            Filter = "Mesh source (.blend, .fbx)|*.blend;*.fbx|Blender project|*.blend|FBX mesh|*.fbx", CheckFileExists = true };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try { MeshWork.UseMeshSource(piece, picker.FileName); Reload(); SetHint(Path.GetExtension(picker.FileName).Equals(".fbx", StringComparison.OrdinalIgnoreCase)
            ? "FBX linked. Re-export this FBX after editing, then Export + Play."
            : "Blender project linked. Ctrl+S saves edits, then Export + Play.", true); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Choose project"); }
    }

    void DeleteMod(ClothingPiece piece)
    {
        if (MessageBox.Show(this, "Delete " + piece.Title + " from your local mods?\n\n"
            + "Your mesh files and textures are kept. Restore it from Deleted mods anytime.\n"
            + "Play removes it from the game, including a subscribed copy. The online gallery is unchanged.",
            "Delete local mod", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try { LocalModTrash.Delete(_tools.Repo, piece); Reload(); SetHint("Deleted " + piece.Title + ". Play applies the removal; Deleted mods lets you restore it.", true); ShowPlayBar(true); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Delete mod"); }
    }

    void BeginExport(ClothingPiece piece, string? bind, bool launchPlay)
    {
        piece = ClothingLibrary.Scan(_tools.Repo).FirstOrDefault(p => p.ItemJson == piece.ItemJson) ?? piece;
        if (!File.Exists(MeshWork.EditableSource(_tools.Repo, piece)))
        {
            MessageBox.Show(this, "Choose your saved .blend or .fbx with Choose mesh… first.", "RoweMod",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (bind == null && !ClothingLibrary.IsSkate(piece))
        {
            MessageBox.Show(this, "Get from game first. Export needs the hoodie bind pose.", "RoweMod",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ExportTarget = piece;
        ExportMesh = true;
        LaunchPlay = launchPlay;
        DialogResult = DialogResult.OK;
        Close();
    }

    readonly ToolTip _tips = new();

    string HelpText(ClothingPiece piece)
    {
        if (!piece.FromGame && _tab == WorkshopTab.Paint)
            return "Edit the PNG and save it, then Play. Use Shape for changes to the mesh.";
        if (!piece.FromGame && piece.Kind == ClothingKind.Mesh)
        {
            if (!File.Exists(MeshWork.EditableSource(_tools.Repo, piece)))
                return "Choose mesh… links a saved .blend or FBX. Open in Blender starts a new saved project.";
            if (string.Equals(Path.GetExtension(piece.Blend), ".fbx", StringComparison.OrdinalIgnoreCase))
                return (piece.Slot == "Frames" ? "" : "Include the game rig and weights. ")
                    + "Re-export this FBX after edits → Export + Play. Ctrl+S in Blender does not update an FBX.";
            return ClothingLibrary.IsSkate(piece)
                ? (piece.Slot == "Boots" ? "Keep the boot rig and weights. " : "Edit your frame in Blender. ") + "Ctrl+S → Export + Play."
                : "Model and weight your garment in Blender. Ctrl+S → Export + Play.";
        }
        if (!piece.FromGame) return "Save the edited PNG, then Play. Delete removes this local mod on the next Play.";
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
                buttons.Height = buttons.GetPreferredSize(new Size(buttons.Width, 0)).Height;
                c.Height = buttons.Top + buttons.Height + 12;
            }
            foreach (Control inner in c.Controls)
            {
                if (inner is Label lab && !lab.AutoSize && lab.Font.Size < 12f)
                    lab.Width = Math.Max(180, w - lab.Left - 16);
                else if (inner is Label wrap && wrap.AutoSize && wrap.Font.Size < 12f)
                    wrap.MaximumSize = new Size(Math.Max(180, w - 32), 0);
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

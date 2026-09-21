namespace RoweMod.App;

enum WorkshopTab
{
    Paint,
    Mesh,
    Templates,
}

sealed class ExportWorkshopForm : Form
{
    readonly DetectedTools _tools;
    List<ClothingPiece> _pieces;
    readonly Label _banner = new();
    readonly Label _hint = new();
    readonly FlowLayoutPanel _list = new();
    readonly List<Panel> _modeCards = new();
    WorkshopTab? _tab;

    public bool ExportMesh { get; private set; }
    public ClothingPiece? ExportTarget { get; private set; }

    public ExportWorkshopForm(DetectedTools tools)
    {
        _tools = tools;
        _pieces = ClothingLibrary.Scan(tools.Repo).ToList();

        Text = "Clothing";
        Width = 920;
        Height = 740;
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10f);

        _banner.Dock = DockStyle.Top;
        _banner.Height = 64;
        _banner.Padding = new Padding(16, 10, 16, 6);
        _banner.Text = "Pick what you are making. The gray bones in Blender are the real game skeleton — there is no hoodie yet until you model one.";

        var pick = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 92,
            ColumnCount = 3,
            Padding = new Padding(12, 0, 12, 8),
        };
        pick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        pick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        pick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        var paint = ModeCard("Paint", "Keep the stock shape. Recolor or paint a texture.");
        var mesh = ModeCard("New mesh", "New shape. Model on the gray skeleton, then export.");
        var templates = ModeCard("Color tints", "Optional extras. Not in the game until you add one.");
        WireClicks(paint, () => SetTab(WorkshopTab.Paint));
        WireClicks(mesh, () => SetTab(WorkshopTab.Mesh));
        WireClicks(templates, () => SetTab(WorkshopTab.Templates));
        _modeCards.AddRange(new[] { paint, mesh, templates });
        pick.Controls.Add(paint, 0, 0);
        pick.Controls.Add(mesh, 1, 0);
        pick.Controls.Add(templates, 2, 0);
        foreach (Control c in pick.Controls) c.Dock = DockStyle.Fill;

        var pullBar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 0, 12, 8) };
        var pull = new Button
        {
            Text = _tools.HasPulledClothing
                ? "Refresh clothes from my game"
                : "Get clothes from my game (for paint + export)",
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
        _hint.Height = 48;
        _hint.Padding = new Padding(16, 4, 16, 4);
        _hint.ForeColor = Color.FromArgb(255, 220, 120);
        _hint.Text = "Start with New mesh if you are making a silhouette. Use Paint to change a texture.";

        _list.Dock = DockStyle.Fill;
        _list.AutoScroll = true;
        _list.WrapContents = false;
        _list.FlowDirection = FlowDirection.TopDown;
        _list.Padding = new Padding(12, 4, 8, 12);
        _list.BackColor = Color.FromArgb(18, 18, 20);

        Controls.Add(_list);
        Controls.Add(_hint);
        Controls.Add(pullBar);
        Controls.Add(pick);
        Controls.Add(_banner);

        var wip = _pieces.FirstOrDefault(p => p.IsEmptyRig);
        SetTab(wip != null ? WorkshopTab.Mesh : WorkshopTab.Paint);
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
        SetHint("Copying clothes from your Steam folder. They stay on this PC.", ok: true);
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
            var n = _pieces.Count(x => x.FromGame);
            if (p.ExitCode == 0 && n > 0)
            {
                pull.Text = "Refresh clothes from my game";
                SetHint("Got " + n + " files from your game. Paint a texture, or look at a mesh for reference.", ok: true);
            }
            else
            {
                SetHint("Could not pull clothes. The game needs to be installed. See dumps/game-clothing/pull.log.", ok: false);
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
    }

    void SetTab(WorkshopTab tab)
    {
        _tab = tab;
        StyleModes();
        _banner.Text = tab switch
        {
            WorkshopTab.Paint => "Same 3D shape as the game. Pull a texture, make a paint mod, edit the PNG, then Pack and Play on the main window.",
            WorkshopTab.Mesh => "You will see a gray stick figure. That is the real Rollout skeleton, not a broken rig. Add your clothing mesh around it, weight-paint, then Export.",
            _ => "These leftover navy/rowe colors are only examples. They do not show in the catalog unless you add one.",
        };
        SetHint(tab switch
        {
            WorkshopTab.Paint => _tools.HasPulledClothing
                ? "Your paint mods are at the top. A pulled texture needs Make a paint mod before the game can wear it."
                : "Click Get clothes from my game, then Make a paint mod on an albedo.",
            WorkshopTab.Mesh => NextMeshHint(),
            _ => "Add a tint only if you want that color in the menu. Otherwise leave these alone.",
        }, ok: true);
        RebuildList();
    }

    string NextMeshHint()
    {
        var wip = _pieces.FirstOrDefault(p => p.IsEmptyRig);
        if (wip != null)
            return "Next: open " + wip.Title + " in Blender, model clothes on the bones, then Export this mesh.";
        if (!_tools.HasPulledClothing)
            return "Get clothes from my game first. Export needs the hoodie bind pose so the mesh does not collapse in-game.";
        return "Create a garment, or export one you already modeled. Game files below are look-only.";
    }

    void RebuildList()
    {
        _list.SuspendLayout();
        _list.Controls.Clear();
        if (_tab == null) { _list.ResumeLayout(); return; }

        if (_tab == WorkshopTab.Mesh)
            _list.Controls.Add(StartMeshCard());

        foreach (var (title, items) in Groups())
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
            yield return ("Ready to wear (after Pack and Play)", _pieces.Where(p => !p.Sample && !p.FromGame && TextureMods.IsPaintTarget(p)).ToList());
            yield return ("Textures from your game", _pieces.Where(p => p.FromGame && TextureMods.IsPaintTarget(p)).ToList());
        }
        else if (_tab == WorkshopTab.Mesh)
        {
            yield return ("Your garments", _pieces.Where(p => !p.Sample && !p.FromGame && p.Kind == ClothingKind.Mesh).ToList());
            yield return ("Look at the real game clothes", _pieces.Where(p => p.Id.StartsWith("game:", StringComparison.OrdinalIgnoreCase)).ToList());
        }
        else
        {
            yield return ("Not in the game yet", _pieces.Where(p => p.Sample).ToList());
        }
    }

    Control StartMeshCard()
    {
        var card = new Panel
        {
            Height = 108,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.FromArgb(36, 56, 44),
            Padding = new Padding(12, 10, 12, 10),
        };
        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font("Segoe UI Semibold", 13f),
            Text = "Create a garment",
        };
        var go = SmallButton("Name it and open Blender", StartNewMesh, primary: true);
        go.Dock = DockStyle.Bottom;
        go.Height = 32;
        go.AutoSize = false;
        var sub = WrapLabel(
            "Makes a new catalog row and a Blender file with only the skeleton. Model the clothes there. Do not edit the shared kit.",
            Color.FromArgb(180, 210, 190));
        sub.Dock = DockStyle.Fill;
        card.Controls.Add(sub);
        card.Controls.Add(go);
        card.Controls.Add(title);
        return card;
    }

    void StartNewMesh()
    {
        using var dlg = new NewMeshForm();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(dlg.GarmentName))
        {
            MessageBox.Show(this, "Give the garment a name.", "RoweMod", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var piece = MeshWork.CreateMesh(_tools.Repo, dlg.GarmentName, dlg.Slot);
            if (_tools.Blender != null && piece.Blend != null)
                ClothingLibrary.OpenInBlender(_tools.Blender, piece.Blend);
            _pieces = ClothingLibrary.Scan(_tools.Repo).ToList();
            SetHint("Blender should show gray bones and no clothes. That is correct. Model the garment, then come back and Export this mesh.", ok: true);
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
            var bind = MeshWork.FindBindGlb(_tools.Repo);
            var exp = SmallButton("Export this mesh", () => BeginExport(piece, bind), primary: true);
            exp.Enabled = piece.Blend != null && bind != null && _tools.Blender != null;
            if (piece.IsEmptyRig)
                _tips.SetToolTip(exp, "Add a clothing mesh in Blender first. Export will fail on an empty skeleton.");
            else if (bind == null)
                _tips.SetToolTip(exp, "Get clothes from my game first (hoodie bind pose).");
            yield return exp;
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
                SetHint("Paint " + made.Value.Title + " in your image app, save the PNG, then Pack and Play.", ok: true);
                RebuildList();
            }, primary: true);
        }

        if (piece.PaintFile != null && File.Exists(piece.PaintFile) && piece.ItemJson != null && !piece.FromGame)
            yield return SmallButton("Edit the PNG", () => ClothingLibrary.OpenFile(piece.PaintFile), primary: _tab == WorkshopTab.Paint);

        if (piece.Sample && piece.ItemJson != null)
        {
            yield return SmallButton("Add this color to the game", () =>
            {
                MeshWork.SetSample(piece.ItemJson, false);
                _pieces = ClothingLibrary.Scan(_tools.Repo).ToList();
                SetHint("Pack and Play will put " + piece.Title + " in the catalog.", ok: true);
                RebuildList();
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
                ClothingLibrary.OpenInBlender(_tools.Blender, blenderFile);
            });
            open.Enabled = _tools.Blender != null;
            yield return open;
        }

        if (!piece.FromGame && piece.ItemJson != null && _tab != WorkshopTab.Mesh)
            yield return SmallButton("Open JSON", () => ClothingLibrary.OpenFile(piece.ItemJson));

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
        if (piece.Sample)
            return piece.Slot + ". Example color only. Add it if you want this tint in the menu.";
        if (piece.Kind == ClothingKind.Kit)
            return "Shared skeleton. Leave this file alone — Create a garment makes your own copy.";
        if (piece.FromGame && piece.Kind == ClothingKind.Mesh)
            return piece.Slot + ". Look-only. Already skinned to the game. Do not start over on weights.";
        if (piece.IsEmptyRig)
            return piece.Slot + ". Skeleton only so far. The stick figure is the real rig. Model clothes around it, then Export.";
        if (piece.HasModeledMesh)
            return piece.Slot + ". Has a mesh. Export if you changed Blender, then Cook and Pack and Play.";
        if (piece.FromGame)
            return piece.Slot + ". Texture from your game. Make a paint mod so Pack and Play can wear your edit.";
        if (piece.PaintFile != null)
            return piece.Slot + ". Edit the PNG, then Pack and Play. Cook runs automatically if needed.";
        return piece.Slot + ". Color or paint. The 3D shape stays the stock game mesh.";
    }

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
            c.Width = w;
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

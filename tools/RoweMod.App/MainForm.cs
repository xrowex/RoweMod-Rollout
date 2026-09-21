using System.Diagnostics;
using System.Text;
using RoweMod.Core;

namespace RoweMod.App;

sealed class MainForm : Form
{
    readonly Button _setup = MakeButton("Setup");
    readonly Button _export = MakeButton("Clothing");
    readonly Button _skates = MakeButton("Skates");
    readonly Button _gallery = MakeButton("Gallery");
    readonly Button _cook = MakeButton("Cook");
    readonly Button _pack = MakeButton("Play");
    ClothingPiece? _exportTarget;
    readonly FlowLayoutPanel _chips = new();
    readonly TextBox _log = new();
    readonly HowToGuide _howTo = new();
    readonly SplitContainer _split = new();
    readonly StatusStrip _status = new();
    readonly ToolStripStatusLabel _exportStatus = new() { Spring = true };
    readonly ToolStripStatusLabel _cookStatus = new() { Spring = true };
    readonly ToolStripStatusLabel _overlayStatus = new() { Spring = true };
    DetectedTools _toolsState;
    bool _busy;

    public HowToGuide HowTo => _howTo;
    public IReadOnlyList<string> Toolbar => new[]
    {
        _setup.Text, _export.Text, _skates.Text, _gallery.Text, _cook.Text, _pack.Text,
    };

    public MainForm()
    {
        _toolsState = ToolPaths.Detect();
        Text = "RoweMod × Rollout";
        Width = 1280;
        Height = 760;
        MinimumSize = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.WhiteSmoke;

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            ColumnCount = 6,
            Padding = new Padding(10, 10, 10, 4),
        };
        for (var i = 0; i < 6; i++)
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
        buttons.Controls.Add(_setup, 0, 0);
        buttons.Controls.Add(_export, 1, 0);
        buttons.Controls.Add(_skates, 2, 0);
        buttons.Controls.Add(_gallery, 3, 0);
        buttons.Controls.Add(_cook, 4, 0);
        buttons.Controls.Add(_pack, 5, 0);
        foreach (Control c in buttons.Controls)
            c.Dock = DockStyle.Fill;

        _chips.Dock = DockStyle.Top;
        _chips.Height = 40;
        _chips.Padding = new Padding(12, 6, 12, 4);
        _chips.WrapContents = false;
        _chips.BackColor = Color.FromArgb(28, 28, 32);

        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Both;
        _log.WordWrap = false;
        _log.BackColor = Color.FromArgb(18, 18, 20);
        _log.ForeColor = Color.FromArgb(210, 230, 220);
        _log.Font = new Font(FontFamily.Families.Any(f => f.Name == "Cascadia Mono") ? "Cascadia Mono" : "Consolas", 9f);
        _log.BorderStyle = BorderStyle.None;

        _howTo.Dock = DockStyle.Fill;
        _howTo.BrowseGameRequested += (_, _) => BrowseGame();
        _howTo.BrowseUnrealRequested += (_, _) => BrowseUnreal();

        _split.Dock = DockStyle.Fill;
        _split.SplitterWidth = 6;
        _split.BackColor = Color.FromArgb(48, 48, 54);
        _split.Panel1MinSize = 80;
        _split.Panel2MinSize = 80;
        _split.Panel1.Controls.Add(_log);
        _split.Panel2.Controls.Add(_howTo);
        Shown += (_, _) =>
        {
            if (_split.Width > 500)
                _split.SplitterDistance = Math.Max(220, _split.Width - 460);
            _split.Panel1MinSize = 220;
            _split.Panel2MinSize = 360;
            TryAutoUpdate();
        };

        _status.SizingGrip = false;
        _status.Items.AddRange(new ToolStripItem[] { _exportStatus, _cookStatus, _overlayStatus });

        Controls.Add(_split);
        Controls.Add(_chips);
        Controls.Add(buttons);
        Controls.Add(_status);
        _status.SendToBack();

        _setup.Click += async (_, _) => await RunJob("Setup", SetupAsync);
        _export.Click += async (_, _) => await OpenExportWorkshop(WorkshopDomain.Clothing);
        _skates.Click += async (_, _) => await OpenExportWorkshop(WorkshopDomain.Skates);
        _gallery.Click += (_, _) => OpenGallery();
        _cook.Click += async (_, _) => await RunJob("Cook", () => CookAsync());
        _pack.Click += async (_, _) => await RunJob("Play", PackAndPlayAsync);

        WireHover(_setup, "setup");
        WireHover(_export, "clothing");
        WireHover(_skates, "skates");
        WireHover(_gallery, "gallery");
        WireHover(_cook, "cook");
        WireHover(_pack, "pack");

        RefreshChrome();
    }

    static Button MakeButton(string text)
    {
        var b = new Button
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 12f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 70, 90),
            ForeColor = Color.White,
            Margin = new Padding(4, 0, 4, 0),
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(90, 140, 160);
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 100, 125);
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 55, 75);
        return b;
    }

    async Task RunJob(string name, Func<Task> work)
    {
        if (_busy) return;
        _busy = true;
        RefreshChrome();
        Log("---- " + name + " ----");
        try
        {
            await work();
            Log(name + " ok");
        }
        catch (Exception ex)
        {
            Log("ERROR " + ex.Message);
        }
        finally
        {
            _toolsState = ToolPaths.Detect();
            _busy = false;
            RefreshChrome();
        }
    }

    void WireHover(Button button, string id)
    {
        button.MouseEnter += (_, _) => _howTo.ShowButton(id);
        button.MouseLeave += (_, _) => _howTo.ShowButton(null);
    }

    void TryAutoUpdate()
    {
        try
        {
            var msg = RepoUpdate.TryPull(_toolsState.Repo);
            if (!string.IsNullOrWhiteSpace(msg))
                Log("Repo  " + msg);
            if (msg != null && msg.StartsWith("updated", StringComparison.OrdinalIgnoreCase))
            {
                _toolsState = ToolPaths.Detect();
                RefreshChrome();
            }
        }
        catch (Exception ex)
        {
            Log("Repo  update skipped: " + ex.Message);
        }
    }

    void BrowseGame()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Pick RolloutInline, RollerSkate, Content, or the Paks folder.",
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var paks = ToolPaths.ResolveGamePaksFromFolder(dlg.SelectedPath);
        if (paks is null)
        {
            MessageBox.Show(
                this,
                "That folder does not look like the game files.\n\nBrowse to:\n…\\RolloutInline\\RollerSkate\\Content\\Paks",
                "RoweMod",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        ToolPaths.RememberGamePaks(paks);
        Log("Game folder " + paks);
        _toolsState = ToolPaths.Detect();
        RefreshChrome();
    }

    void BrowseUnreal()
    {
        using var fileDlg = new OpenFileDialog
        {
            Title = "Pick UnrealEditor.exe (UE 5.4)",
            Filter = "Unreal Editor|UnrealEditor.exe|All files|*.*",
            FileName = "UnrealEditor.exe",
            CheckFileExists = true,
        };
        if (fileDlg.ShowDialog(this) != DialogResult.OK) return;

        var editor = ToolPaths.ResolveUnrealFromPath(fileDlg.FileName);
        if (editor is null)
        {
            MessageBox.Show(
                this,
                "That is not Unreal Engine 5.4.\n\nBrowse to:\n…\\UE_5.4\\Engine\\Binaries\\Win64\\UnrealEditor.exe",
                "RoweMod",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        ToolPaths.RememberUnrealEditor(editor);
        Log("Unreal  " + editor);
        _toolsState = ToolPaths.Detect();
        RefreshChrome();
    }

    async Task SetupAsync()
    {
        var extra = _toolsState.GamePaks is null ? Array.Empty<string>() : new[] { "-GamePaks", "\"" + _toolsState.GamePaks + "\"" };
        var code = await RunPowerShell(_toolsState.BootstrapScript, extra);
        if (code != 0) throw new InvalidOperationException("Setup failed (" + code + ")");
    }

    void OpenGallery()
    {
        using var dlg = new GalleryForm(_toolsState);
        dlg.ShowDialog(this);
        _toolsState = ToolPaths.Detect();
        RefreshChrome();
    }

    async Task OpenExportWorkshop(WorkshopDomain domain)
    {
        using var dlg = new ExportWorkshopForm(_toolsState, domain);
        var result = dlg.ShowDialog(this);
        _toolsState = ToolPaths.Detect();
        RefreshChrome();
        if (dlg.SavedCookTarget)
            Log("Cook target saved.");
        if (result != DialogResult.OK || !dlg.ExportMesh)
            return;
        _exportTarget = dlg.ExportTarget;
        await RunJob("Export", ExportMeshAsync);
    }

    async Task ExportMeshAsync()
    {
        if (_toolsState.Blender is null)
            throw new InvalidOperationException("Blender 5.1 not found.");
        var target = _exportTarget != null
            ? MeshWork.FromPiece(_toolsState.Repo, _exportTarget)
            : MeshWork.LoadOrDefault(_toolsState.Repo);
        if (string.IsNullOrWhiteSpace(target.Blend) || !File.Exists(target.Blend))
            throw new InvalidOperationException("No Blender file for this garment yet.");
        if (string.IsNullOrWhiteSpace(target.BindGlb) || !File.Exists(target.BindGlb))
            throw new InvalidOperationException("Pull clothing first. Export needs the game hoodie bind pose.");
        MeshWork.SaveTarget(_toolsState.Repo, target);
        Log("Export " + target.MeshName + " from " + target.Blend);
        var code = await RunProcess(
            _toolsState.Blender,
            "-b --factory-startup -P \"" + _toolsState.ExportScript + "\"",
            _toolsState.Repo,
            MeshWork.ExportEnv(target));
        if (code != 0) throw new InvalidOperationException("Export failed (" + code + ")");
        if (File.Exists(target.Gamebind))
            Log("Export glb " + target.Gamebind + " " + File.GetLastWriteTime(target.Gamebind));
    }

    async Task CookAsync(bool packing = false)
    {
        if (_toolsState.UnrealEditor is null)
            throw new InvalidOperationException("Unreal Engine 5.4.4 not found.");
        var code = await RunPowerShell(_toolsState.CookScript, "-SkipPack");
        if (code != 0) throw new InvalidOperationException("Cook failed (" + code + ")");
        if (!packing)
            Log("Cook finished.");
    }

    async Task PackAndPlayAsync()
    {
        if (TextureMods.NeedsCook(_toolsState.Repo))
        {
            if (_toolsState.UnrealEditor is null)
                throw new InvalidOperationException("Painted textures need Unreal 5.4.4 to Cook before Play.");
            Log("Paint is newer than the last cook. Cooking first...");
            await CookAsync(packing: true);
        }

        foreach (var p in Process.GetProcessesByName("RollerSkate"))
        {
            Log("Stopping " + p.ProcessName);
            p.Kill(true);
        }

        if (_toolsState.GamePaks is null)
            throw new InvalidOperationException("Game folder not found.");

        Log("Syncing subscribed gallery mods...");
        try
        {
            await new GalleryClient().SyncSubscriptionsAsync(_toolsState.Repo, Log);
        }
        catch (Exception ex)
        {
            Log("Gallery sync skipped: " + ex.Message);
        }

        await Task.Run(() => ModMerge.Pack(_toolsState.Repo, _toolsState.GamePaks, Log));

        var overlay = Path.Combine(_toolsState.GamePaks, "RollerSkate-Windows_P.utoc");
        if (File.Exists(overlay))
            Log("Overlay " + overlay);

        Log("Launching steam://run/4464990");
        ModMerge.LaunchGame();
    }

    Task<int> RunPowerShell(string script, params string[] extra) =>
        RunProcess(
            "powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" " + string.Join(" ", extra),
            _toolsState.Repo,
            ToolEnv());

    Dictionary<string, string> ToolEnv()
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(_toolsState.GamePaks))
            env["ROWE_GAME_PAKS"] = _toolsState.GamePaks;
        if (!string.IsNullOrWhiteSpace(_toolsState.UnrealEditor))
            env["ROWE_UE54"] = _toolsState.UnrealEditor;
        if (!string.IsNullOrWhiteSpace(_toolsState.Blender))
            env["ROWE_BLENDER"] = _toolsState.Blender;
        return env;
    }

    Task<int> RunProcess(string file, string args, string cwd, IReadOnlyDictionary<string, string>? env = null)
    {
        return Task.Run(() =>
        {
            var psi = new ProcessStartInfo(file, args)
            {
                WorkingDirectory = cwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var kv in ToolEnv())
                psi.Environment[kv.Key] = kv.Value;
            if (env != null)
            {
                foreach (var kv in env)
                    psi.Environment[kv.Key] = kv.Value;
            }
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start " + file);
            p.OutputDataReceived += (_, e) => { if (e.Data != null) Log(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            return p.ExitCode;
        });
    }

    sealed class LogTextWriter : TextWriter
    {
        readonly Action<string> _write;
        readonly StringBuilder _buf = new();
        public LogTextWriter(Action<string> write) => _write = write;
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value)
        {
            if (value is '\n')
            {
                _write(_buf.ToString().TrimEnd('\r'));
                _buf.Clear();
                return;
            }
            _buf.Append(value);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && _buf.Length > 0)
                _write(_buf.ToString());
            base.Dispose(disposing);
        }
    }

    void RefreshChrome()
    {
        var t = _toolsState;
        var setupDone = t.HasRetoc && t.GamePaks != null;
        _setup.Enabled = !_busy && !setupDone;
        StyleSetupButton(setupDone);
        _export.Enabled = !_busy;
        _skates.Enabled = !_busy;
        _gallery.Enabled = !_busy;
        _cook.Enabled = !_busy && t.UnrealEditor != null;
        _pack.Enabled = !_busy && t.GamePaks != null && t.HasRetoc && t.HasDotnet;

        RebuildChips(t, setupDone);
        _howTo.Bind(t);

        _exportStatus.Text = t.LastExport is { } exp
            ? "Export  " + exp.ToString("g")
            : "Export  —";
        _cookStatus.Text = t.LastCook is { } cook
            ? "Cook  " + cook.ToString("g")
            : "Cook  —";
        _overlayStatus.Text = t.OverlayUtoc != null ? "In the game" : "Not in the game yet";
    }

    void StyleSetupButton(bool done)
    {
        if (done)
        {
            _setup.Text = "Setup done";
            _setup.BackColor = Color.FromArgb(38, 46, 40);
            _setup.ForeColor = Color.FromArgb(130, 150, 135);
            _setup.FlatAppearance.BorderColor = Color.FromArgb(70, 110, 75);
            _setup.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 46, 40);
            _setup.Cursor = Cursors.Default;
            return;
        }
        _setup.Text = "Setup";
        _setup.BackColor = Color.FromArgb(45, 70, 90);
        _setup.ForeColor = Color.White;
        _setup.FlatAppearance.BorderColor = Color.FromArgb(90, 140, 160);
        _setup.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 100, 125);
        _setup.Cursor = Cursors.Hand;
    }

    void RebuildChips(DetectedTools t, bool setupDone)
    {
        _chips.SuspendLayout();
        _chips.Controls.Clear();
        _chips.Controls.Add(Chip(setupDone ? "Setup" : "Setup needed", setupDone, true));
        _chips.Controls.Add(Chip(
            t.GamePaks != null ? "Game" : "No game",
            t.GamePaks != null,
            false,
            t.GamePaks is null ? BrowseGame : null));
        _chips.Controls.Add(Chip(t.HasPulledClothing ? "Pulled" : "Not pulled", t.HasPulledClothing, false));
        _chips.Controls.Add(Chip(t.Blender != null ? "Blender" : "No Blender", t.Blender != null, false));
        _chips.Controls.Add(Chip(
            t.UnrealEditor != null ? "Unreal" : "No Unreal",
            t.UnrealEditor != null,
            false,
            t.UnrealEditor is null ? BrowseUnreal : null));
        _chips.ResumeLayout();
    }

    static Label Chip(string text, bool ok, bool emphasize, Action? onClick = null)
    {
        var green = emphasize && ok;
        var chip = new Label
        {
            Text = (ok ? "  ●  " : "  ○  ") + text,
            AutoSize = true,
            Padding = new Padding(8, 5, 10, 5),
            Margin = new Padding(0, 0, 8, 0),
            BackColor = green
                ? Color.FromArgb(28, 72, 42)
                : ok ? Color.FromArgb(36, 44, 40) : Color.FromArgb(48, 36, 34),
            ForeColor = green
                ? Color.FromArgb(150, 235, 170)
                : ok ? Color.FromArgb(170, 200, 185) : Color.FromArgb(210, 170, 150),
            Font = new Font("Segoe UI Semibold", 9f),
            Cursor = onClick != null ? Cursors.Hand : Cursors.Default,
        };
        if (onClick != null)
            chip.Click += (_, _) => onClick();
        return chip;
    }

    void Log(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(line));
            return;
        }
        _log.AppendText(line + Environment.NewLine);
    }
}

using RoweMod.Core;

namespace RoweMod.Play;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SplashForm());
    }
}

sealed class SplashForm : Form
{
    readonly Label _status = new();
    readonly TextBox _log = new();

    public SplashForm()
    {
        Text = "RoweMod Play";
        Width = 520;
        Height = 320;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10f);

        _status.Dock = DockStyle.Top;
        _status.Height = 48;
        _status.Padding = new Padding(16, 14, 16, 8);
        _status.Font = new Font("Segoe UI Semibold", 13f);
        _status.Text = "Updating mods…";

        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.BackColor = Color.FromArgb(18, 18, 20);
        _log.ForeColor = Color.FromArgb(200, 220, 210);
        _log.BorderStyle = BorderStyle.None;
        _log.ScrollBars = ScrollBars.Vertical;

        Controls.Add(_log);
        Controls.Add(_status);
        Shown += async (_, _) => await RunAsync();
    }

    async Task RunAsync()
    {
        try
        {
            var repo = DtPatcher.Program.DiscoverRepo();
            Log("Repo " + repo);
            var paks = GameLocate.FindPaks();
            if (paks == null)
                throw new InvalidOperationException("Rollout Paks folder not found. Open RoweMod and click Setup.");

            _status.Text = "Downloading subscribed mods…";
            var client = new GalleryClient();
            await client.SyncSubscriptionsAsync(repo, Log);

            _status.Text = "Writing overlay…";
            await Task.Run(() => ModMerge.Pack(repo, paks, Log));

            _status.Text = "Launching Rollout…";
            ModMerge.LaunchGame();
            await Task.Delay(800);
            Close();
        }
        catch (Exception ex)
        {
            _status.Text = "Could not start";
            Log("ERROR " + ex.Message);
        }
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

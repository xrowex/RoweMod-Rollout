using System.Text;

namespace RoweMod.App;

// Runs without touching the game or requiring extracted assets.
static class PortableChecks
{
    public static int Run(string reportDirectory)
    {
        Directory.CreateDirectory(reportDirectory);
        var log = new StringBuilder();
        try
        {
            var runtime = typeof(object).Assembly.Location;
            if (!Path.GetFullPath(runtime).StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Runtime was not loaded from the portable app folder: " + runtime);
            log.AppendLine("OK bundled runtime: " + runtime);
            ApplicationConfiguration.Initialize();
            using var main = new MainForm();
            main.StartPosition = FormStartPosition.Manual;
            main.Location = new Point(-2000, -2000);
            main.Show();
            Application.DoEvents();
            if (main.Toolbar.Count != 4) throw new InvalidOperationException("Main app did not load");
            using var image = new Bitmap(main.Width, main.Height);
            main.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save(Path.Combine(reportDirectory, "portable-main.png"));
            log.AppendLine("OK main window rendered");
            var ready = new DetectedTools { Repo = ToolPaths.RepoRoot, GamePaks = "test-game", HasRetoc = true };
            if (UiCopy.NextStep(ready).Action != UiCopy.NextAction.Play)
                throw new InvalidOperationException("Clean player install did not offer Play");
            WorkshopChecks.Run(ToolPaths.RepoRoot, line => log.AppendLine("OK " + line));
            log.AppendLine("RESULT pass");
            File.WriteAllText(Path.Combine(reportDirectory, "portable-check.txt"), log.ToString());
            return 0;
        }
        catch (Exception ex)
        {
            log.AppendLine("FAIL " + ex);
            File.WriteAllText(Path.Combine(reportDirectory, "portable-check.txt"), log.ToString());
            return 1;
        }
    }
}

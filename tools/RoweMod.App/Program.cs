namespace RoweMod.App;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Dump(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Dump(ex);
        };
        try
        {
            if (args.Any(a => a.Equals("--check", StringComparison.OrdinalIgnoreCase)))
                return FlowPlaytest.Run();

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
            return 0;
        }
        catch (Exception ex)
        {
            Dump(ex);
            return 1;
        }
    }

    static void Dump(Exception ex)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "rowemod-crash.log");
            File.WriteAllText(path, DateTime.Now + Environment.NewLine + ex);
            MessageBox.Show(ex.Message + "\n\n" + path, "RoweMod", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            MessageBox.Show(ex.ToString(), "RoweMod");
        }
    }
}

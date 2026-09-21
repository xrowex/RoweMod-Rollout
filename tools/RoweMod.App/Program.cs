namespace RoweMod.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Dump(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Dump(ex);
        };
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            Dump(ex);
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

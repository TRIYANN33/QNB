namespace QNB;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.AddMessageFilter(new DuplicateButtonMessageFilter());
        Application.Run(new MainForm());
    }
}

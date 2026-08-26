namespace PandaPro8K;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using var instanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Global\PandaPro8KBridge-SingleInstance",
            createdNew: out bool isFirstInstance);

        if (!isFirstInstance)
        {
            MessageBox.Show(
                "Panda Pro 8K is already running.",
                "Panda Pro 8K",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        bool startMinimized = args.Contains("--startup", StringComparer.OrdinalIgnoreCase);
        bool steamFilterReady = args.Contains("--steam-filter-ready", StringComparer.OrdinalIgnoreCase) ||
            (startMinimized && StartupManager.IsGameSirIgnored());
        Application.Run(new MainForm(startMinimized, steamFilterReady));
    }
}

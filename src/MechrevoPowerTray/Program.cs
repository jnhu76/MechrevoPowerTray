using MechrevoPowerTray.App;

namespace MechrevoPowerTray;

internal static class Program
{
    private const string MutexName = @"Local\MechrevoPowerTray.SingleInstance";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(
            initiallyOwned: true,
            name: MutexName,
            createdNew: out var createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "Mechrevo Power Tray 已经在运行。",
                "Mechrevo Power Tray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext());
        GC.KeepAlive(mutex);
    }
}

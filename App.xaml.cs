using System.Windows;

namespace SolarPowerMonitor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            Shutdown(ProtocolSelfTests.Run());
            return;
        }

        base.OnStartup(e);
        new MainWindow(e.Args).Show();
    }
}

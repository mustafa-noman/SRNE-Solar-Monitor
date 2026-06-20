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
        var widgetMode = e.Args.Contains("--widget", StringComparer.OrdinalIgnoreCase);
        var appArgs = e.Args
            .Where(argument => !argument.Equals("--widget", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (widgetMode)
        {
            new WidgetWindow(appArgs).Show();
        }
        else
        {
            new MainWindow(appArgs).Show();
        }
    }
}

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SolarPowerMonitor;

public partial class WidgetWindow : Window
{
    private readonly string[] _args;
    private readonly AppOptions _options;
    private CancellationTokenSource? _monitorCancellation;

    public WidgetWindow(string[] args)
    {
        InitializeComponent();
        _args = args;
        _options = AppOptions.Parse(args);
        Loaded += WidgetWindow_Loaded;
        Closed += WidgetWindow_Closed;
    }

    private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 20;
        Top = workArea.Bottom - ActualHeight - 20;

        _monitorCancellation = new CancellationTokenSource();
        _ = MonitorAsync(_monitorCancellation.Token);
    }

    private void WidgetWindow_Closed(object? sender, EventArgs e)
    {
        _monitorCancellation?.Cancel();
        _monitorCancellation?.Dispose();
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OpenDashboard_Click(object sender, RoutedEventArgs e)
    {
        var dashboard = new MainWindow(_args);
        dashboard.Show();
        dashboard.Activate();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var client = new SrneCloudClient(
                _options.DeviceId,
                _options.ConnectTimeout,
                _options.ResponseTimeout);

            try
            {
                SetStatus("CONNECTING", "#FFB547");
                await client.ConnectAsync(cancellationToken);
                SetStatus("ONLINE", "#35D49A");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var registers = await client.ReadTelemetryAsync(cancellationToken);
                    UpdateTelemetry(SolarTelemetry.FromRegisters(registers));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                SetStatus("RECONNECTING", "#FF6B72");

                try
                {
                    await Task.Delay(_options.ReconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private void UpdateTelemetry(SolarTelemetry telemetry)
    {
        Dispatcher.Invoke(() =>
        {
            SolarPowerText.Text = telemetry.PvChargingPower.ToString();
            BatterySocText.Text = telemetry.BatteryStateOfCharge.ToString();
            DetailsText.Text =
                $"{telemetry.PvArrayVoltage:0.0} V  •  {telemetry.PvArrayCurrent:0.00} A  •  Battery {telemetry.BatteryVoltage:0.0} V";
            LastUpdateText.Text = DateTime.Now.ToString("HH:mm");
        });
    }

    private void SetStatus(string text, string color)
    {
        Dispatcher.Invoke(() =>
        {
            var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
            StatusText.Text = text;
            StatusText.Foreground = brush;
            StatusDot.Fill = brush;
        });
    }
}

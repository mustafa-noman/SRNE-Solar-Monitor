using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SolarPowerMonitor;

public partial class MainWindow : Window
{
    private readonly AppOptions _options;
    private readonly ObservableCollection<decimal> _powerSamples = [];
    private CancellationTokenSource? _monitorCancellation;

    public MainWindow(string[] args)
    {
        InitializeComponent();
        _options = AppOptions.Parse(args);
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StartMonitor();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _monitorCancellation?.Cancel();
        _monitorCancellation?.Dispose();
    }

    private void Reconnect_Click(object sender, RoutedEventArgs e)
    {
        StartMonitor();
    }

    private void StartMonitor()
    {
        _monitorCancellation?.Cancel();
        _monitorCancellation?.Dispose();
        _monitorCancellation = new CancellationTokenSource();
        _ = MonitorAsync(_monitorCancellation.Token);
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        SetStatus("CONNECTING", "#FFB547", "#3C2D18");

        if (_options.Source == MonitorSource.Direct)
        {
            await MonitorDirectAsync(cancellationToken);
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var client = new SrneCloudClient(
                _options.DeviceId,
                _options.ConnectTimeout,
                _options.ResponseTimeout);

            try
            {
                await client.ConnectAsync(cancellationToken);
                SetStatus("ONLINE", "#35D49A", "#102A25");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var registers = await client.ReadTelemetryAsync(cancellationToken);
                    var telemetry = SolarTelemetry.FromRegisters(registers);
                    UpdateTelemetry(telemetry);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                SetStatus("RECONNECTING", "#FF6B72", "#351B24");

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

    private async Task MonitorDirectAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var client = new ModbusRtuTcpClient(
                _options.Host,
                _options.Port,
                _options.SlaveId,
                _options.ConnectTimeout,
                _options.ResponseTimeout);

            try
            {
                await client.ConnectAsync(cancellationToken);
                SetStatus("ONLINE", "#35D49A", "#102A25");
                using var timer = new PeriodicTimer(_options.PollInterval);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var registers = await client.ReadHoldingRegistersAsync(
                        AppOptions.StartRegister,
                        AppOptions.RegisterCount,
                        cancellationToken);
                    UpdateTelemetry(SolarTelemetry.FromRegisters(registers));

                    if (!await timer.WaitForNextTickAsync(cancellationToken))
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                SetStatus("RECONNECTING", "#FF6B72", "#351B24");

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
            PvVoltageText.Text = $"{telemetry.PvArrayVoltage:0.0} V";
            PvCurrentText.Text = $"{telemetry.PvArrayCurrent:0.00} A";
            SolarModeText.Text = telemetry.PvChargingPower > 0
                ? "Solar array is charging the battery"
                : "Solar input is idle";

            BatterySocText.Text = telemetry.BatteryStateOfCharge.ToString();
            BatteryVoltageText.Text = $"{telemetry.BatteryVoltage:0.0} V";
            BatteryCurrentText.Text = $"{telemetry.BatteryChargingCurrent:0.00} A";
            BatteryStateText.Text = telemetry.BatteryChargingCurrent > 0
                ? "Charging normally"
                : "Battery standing by";
            BatteryRing.StrokeDashArray = new DoubleCollection
            {
                Math.Clamp((int)telemetry.BatteryStateOfCharge, 0, 100),
                100
            };

            LastUpdateText.Text = DateTime.Now.ToString("HH:mm:ss");

            _powerSamples.Add(telemetry.PvChargingPower);
            while (_powerSamples.Count > 24)
            {
                _powerSamples.RemoveAt(0);
            }

            DrawChart();
        });
    }

    private void SetStatus(string text, string foreground, string background)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = text;
            StatusText.Foreground = (Brush)new BrushConverter().ConvertFromString(foreground)!;
            StatusDot.Fill = StatusText.Foreground;

            if (StatusText.Parent is StackPanel panel &&
                panel.Parent is Border border)
            {
                border.Background =
                    (Brush)new BrushConverter().ConvertFromString(background)!;
            }
        });
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawChart();
    }

    private void DrawChart()
    {
        if (_powerSamples.Count == 0 ||
            ChartCanvas.ActualWidth <= 0 ||
            ChartCanvas.ActualHeight <= 0)
        {
            return;
        }

        var max = Math.Max(10m, _powerSamples.Max());
        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;
        var points = new PointCollection();

        for (var index = 0; index < _powerSamples.Count; index++)
        {
            var x = _powerSamples.Count == 1
                ? 0
                : index * width / (_powerSamples.Count - 1);
            var y = height - ((double)(_powerSamples[index] / max) * (height - 10)) - 5;
            points.Add(new Point(x, y));
        }

        PowerChartLine.Points = points;
        ChartRangeText.Text =
            $"{_powerSamples.Min():0}–{_powerSamples.Max():0} W  •  {_powerSamples.Count} samples";
    }
}

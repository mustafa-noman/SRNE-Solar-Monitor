using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SolarPowerMonitor.Mobile.Models;
using SolarPowerMonitor.Mobile.Services;

namespace SolarPowerMonitor.Mobile;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    public static DashboardViewModel Current { get; } = new();
    private CancellationTokenSource? cancellation;
    private SolarTelemetry telemetry = new(100, 36.4m, 1.01m, 37, 14.0m, 2.57m);
    private string statusText = "Demo";
    private string statusColor = "#FFAF3F";
    private string errorMessage = "";
    private DateTime lastUpdate = DateTime.Now;

    private DashboardViewModel() => RefreshCommand = new Command(async () => await RefreshAsync());

    public ObservableCollection<HistoryPoint> History { get; } = [];
    public ICommand RefreshCommand { get; }
    public ushort SolarPower => telemetry.PvChargingPower;
    public ushort BatteryPercent => telemetry.BatteryStateOfCharge;
    public string PvVoltageText => $"{telemetry.PvArrayVoltage:F1} V";
    public string PvCurrentText => $"{telemetry.PvArrayCurrent:F2} A";
    public string BatteryVoltageText => $"{telemetry.BatteryVoltage:F1} V";
    public string ChargeCurrentText => $"{telemetry.BatteryChargingCurrent:F2} A";
    public string DeviceName => MonitorSettings.DeviceName;
    public string DataSourceText => MonitorSettings.DemoMode ? "Demo data" : "Direct LAN Modbus";
    public string LastUpdateText => lastUpdate.ToString("HH:mm:ss");
    public string StatusText { get => statusText; private set => Set(ref statusText, value); }
    public string StatusColor { get => statusColor; private set => Set(ref statusColor, value); }
    public string ErrorMessage { get => errorMessage; private set { Set(ref errorMessage, value); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public Task StartAsync()
    {
        if (cancellation is not null) return Task.CompletedTask;
        cancellation = new CancellationTokenSource();
        _ = PollAsync(cancellation.Token);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
    }

    public async Task RestartAsync()
    {
        Stop();
        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(DataSourceText));
        await StartAsync();
    }

    private async Task PollAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await RefreshAsync(token);
            try { await Task.Delay(TimeSpan.FromSeconds(3), token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private Task RefreshAsync() => RefreshAsync(cancellation?.Token ?? CancellationToken.None);

    private async Task RefreshAsync(CancellationToken token)
    {
        try
        {
            SolarTelemetry next;
            if (MonitorSettings.DemoMode)
            {
                var watts = (ushort)Random.Shared.Next(35, 42);
                next = telemetry with { PvChargingPower = watts, PvArrayCurrent = watts / Math.Max(telemetry.PvArrayVoltage, 1) };
                StatusText = "Demo";
                StatusColor = "#FFAF3F";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(MonitorSettings.Host))
                    throw new InvalidOperationException("Set controller host in Settings.");
                next = await ModbusSolarClient.ReadAsync(MonitorSettings.Host, MonitorSettings.Port, checked((byte)MonitorSettings.SlaveId), token);
                StatusText = "Online";
                StatusColor = "#38D39F";
            }

            telemetry = next;
            lastUpdate = DateTime.Now;
            ErrorMessage = "";
            History.Insert(0, new HistoryPoint(lastUpdate, next.PvChargingPower, next.BatteryStateOfCharge));
            while (History.Count > 50) History.RemoveAt(History.Count - 1);
            NotifyTelemetry();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            StatusText = "Offline";
            StatusColor = "#FF7B7B";
            ErrorMessage = ex.Message;
        }
    }

    private void NotifyTelemetry()
    {
        foreach (var property in new[] { nameof(SolarPower), nameof(BatteryPercent), nameof(PvVoltageText), nameof(PvCurrentText), nameof(BatteryVoltageText), nameof(ChargeCurrentText), nameof(LastUpdateText) })
            OnPropertyChanged(property);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }
}

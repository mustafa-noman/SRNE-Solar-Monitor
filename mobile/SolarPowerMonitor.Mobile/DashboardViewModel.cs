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
    private readonly CloudSolarConnection cloudConnection = new();
    private CancellationTokenSource? cancellation;
    private SolarTelemetry telemetry = new(0, 0, 0, 0, 0, 0);
    private string statusText = "Connecting";
    private string statusColor = "#91A4BF";
    private string errorMessage = "";
    private DateTime lastUpdate = DateTime.Now;

    private DashboardViewModel() => RefreshCommand = new Command(async () => await RefreshAsync());

    public ObservableCollection<HistoryPoint> History { get; } = [];
    public ICommand RefreshCommand { get; }
    public ushort SolarPower => telemetry.PvChargingPower;
    public ushort BatteryPercent => telemetry.BatteryStateOfCharge;
    public string SolarStatusText => telemetry.PvChargingPower > 0 ? "Solar array is charging the battery" : "No solar charging detected";
    public string BatteryStatusText => telemetry.BatteryChargingCurrent > 0 ? "Charging normally" : "Not charging";
    public string PvVoltageText => $"{telemetry.PvArrayVoltage:F1} V";
    public string PvCurrentText => $"{telemetry.PvArrayCurrent:F2} A";
    public string BatteryVoltageText => $"{telemetry.BatteryVoltage:F1} V";
    public string ChargeCurrentText => $"{telemetry.BatteryChargingCurrent:F2} A";
    public string DeviceName => MonitorSettings.DeviceName;
    public string DataSourceText => MonitorSettings.Mode switch
    {
        MonitorMode.Cloud => "SRNE cloud telemetry",
        MonitorMode.Direct => "Direct LAN Modbus",
        _ => "Demo data"
    };
    public string LastUpdateText => lastUpdate.ToString("HH:mm:ss");
    public string StatusText { get => statusText; private set => Set(ref statusText, value); }
    public string StatusColor { get => statusColor; private set => Set(ref statusColor, value); }
    public string ErrorMessage { get => errorMessage; private set { Set(ref errorMessage, value); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task StartAsync()
    {
        if (cancellation is not null) return;
        await MonitorSettings.InitializeAsync();
        var storedHistory = await SolarDatabase.GetHistoryAsync();
        History.Clear();
        foreach (var item in storedHistory)
            History.Add(item);
        cancellation = new CancellationTokenSource();
        _ = PollAsync(cancellation.Token);
    }

    public void Stop()
    {
        cancellation?.Cancel();
        cancellation = null;
    }

    public async Task RestartAsync()
    {
        Stop();
        await cloudConnection.ResetAsync();
        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(DataSourceText));
        await StartAsync();
    }

    private async Task PollAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var succeeded = await RefreshAsync(token);
            var delay = succeeded && MonitorSettings.Mode == MonitorMode.Cloud
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(2);
            try
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, token);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RefreshAsync() =>
        _ = await RefreshAsync(cancellation?.Token ?? CancellationToken.None);

    private async Task<bool> RefreshAsync(CancellationToken token)
    {
        try
        {
            SolarTelemetry next;
            if (MonitorSettings.Mode == MonitorMode.Demo)
            {
                var watts = (ushort)Random.Shared.Next(35, 42);
                next = telemetry with { PvChargingPower = watts, PvArrayCurrent = watts / Math.Max(telemetry.PvArrayVoltage, 1) };
                StatusText = "Demo";
                StatusColor = "#FFAF3F";
            }
            else if (MonitorSettings.Mode == MonitorMode.Direct)
            {
                if (string.IsNullOrWhiteSpace(MonitorSettings.Host))
                    throw new InvalidOperationException("Set controller host in Settings.");
                next = await ModbusSolarClient.ReadAsync(MonitorSettings.Host, MonitorSettings.Port, checked((byte)MonitorSettings.SlaveId), token);
                StatusText = "Online";
                StatusColor = "#38D39F";
            }
            else
            {
                var deviceId = await MonitorSettings.GetDeviceIdAsync();
                if (deviceId.Length != 8 || !deviceId.All(Uri.IsHexDigit))
                    throw new InvalidOperationException("Enter your eight-character SRNE device ID in Settings.");
                if (StatusText != "Online")
                {
                    StatusText = "Connecting";
                    StatusColor = "#91A4BF";
                }
                next = await cloudConnection.ReadAsync(deviceId, token);
                StatusText = "Online";
                StatusColor = "#38D39F";
            }

            telemetry = next;
            var recordedAt = DateTimeOffset.Now;
            lastUpdate = recordedAt.LocalDateTime;
            await SolarDatabase.InsertTelemetryAsync(
                next,
                MonitorSettings.Mode.ToString(),
                "foreground",
                recordedAt);
            ErrorMessage = "";
            History.Insert(0, new HistoryPoint(lastUpdate, next.PvChargingPower, next.BatteryStateOfCharge));
            while (History.Count > 500) History.RemoveAt(History.Count - 1);
            NotifyTelemetry();
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
        catch (Exception ex)
        {
            StatusText = "Offline";
            StatusColor = "#FF7B7B";
            ErrorMessage = ex.Message;
            return false;
        }
    }

    private void NotifyTelemetry()
    {
        foreach (var property in new[] { nameof(SolarPower), nameof(BatteryPercent), nameof(SolarStatusText), nameof(BatteryStatusText), nameof(PvVoltageText), nameof(PvCurrentText), nameof(BatteryVoltageText), nameof(ChargeCurrentText), nameof(LastUpdateText) })
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

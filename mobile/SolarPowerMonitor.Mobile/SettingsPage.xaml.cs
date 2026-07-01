using SolarPowerMonitor.Mobile.Services;

namespace SolarPowerMonitor.Mobile;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        await MonitorSettings.InitializeAsync();
        SourcePicker.SelectedIndex = MonitorSettings.Mode switch
        {
            MonitorMode.Cloud => 0,
            MonitorMode.Direct => 1,
            _ => 2
        };
        DeviceNameEntry.Text = MonitorSettings.DeviceName;
        DeviceIdEntry.Text = await MonitorSettings.GetDeviceIdAsync();
        HostEntry.Text = MonitorSettings.Host;
        PortEntry.Text = MonitorSettings.Port.ToString();
        SlaveEntry.Text = MonitorSettings.SlaveId.ToString();
        UpdateSourceVisibility();
        await RefreshStorageStatusAsync();
    }

    private void OnSourceChanged(object? sender, EventArgs e) => UpdateSourceVisibility();

    private void UpdateSourceVisibility()
    {
        CloudSettings.IsVisible = SourcePicker.SelectedIndex == 0;
        DirectSettings.IsVisible = SourcePicker.SelectedIndex == 1;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var mode = SourcePicker.SelectedIndex switch
        {
            0 => MonitorMode.Cloud,
            1 => MonitorMode.Direct,
            _ => MonitorMode.Demo
        };

        var deviceId = DeviceIdEntry.Text?.Trim().ToUpperInvariant() ?? "";
        if (mode == MonitorMode.Cloud && (deviceId.Length != 8 || !deviceId.All(Uri.IsHexDigit)))
        {
            ShowError("Device ID must contain exactly eight hexadecimal characters.");
            return;
        }

        if (!int.TryParse(PortEntry.Text, out var port) || port is < 1 or > 65535 ||
            !int.TryParse(SlaveEntry.Text, out var slave) || slave is < 1 or > 255)
        {
            ShowError("Port must be 1–65535 and slave ID 1–255.");
            return;
        }

        var configuration = new MonitorConfiguration(
            mode,
            string.IsNullOrWhiteSpace(DeviceNameEntry.Text) ? "SolarHub" : DeviceNameEntry.Text.Trim(),
            HostEntry.Text?.Trim() ?? "",
            port,
            slave);
        await MonitorSettings.SaveAsync(configuration);
        if (mode == MonitorMode.Cloud)
            await MonitorSettings.SetDeviceIdAsync(deviceId);

        await DashboardViewModel.Current.RestartAsync();
        ResultLabel.TextColor = Color.FromArgb("#38D39F");
        ResultLabel.Text = "Saved. Monitor reconnecting.";
        await RefreshStorageStatusAsync();
    }

    private async Task RefreshStorageStatusAsync()
    {
        var stats = await SolarDatabase.GetStatsAsync();
        var lastBackground = stats.LastBackgroundUtc is null
            ? "not yet"
            : stats.LastBackgroundUtc.Value.ToLocalTime().ToString("g");
        StorageStatusLabel.Text = $"Stored readings: {stats.ReadingCount:N0}  •  Last background: {lastBackground}";
    }

    private void ShowError(string message)
    {
        ResultLabel.TextColor = Color.FromArgb("#FF7B7B");
        ResultLabel.Text = message;
    }
}

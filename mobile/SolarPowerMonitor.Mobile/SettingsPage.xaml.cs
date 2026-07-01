using SolarPowerMonitor.Mobile.Services;

namespace SolarPowerMonitor.Mobile;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        DemoSwitch.IsToggled = MonitorSettings.DemoMode;
        DeviceNameEntry.Text = MonitorSettings.DeviceName;
        HostEntry.Text = MonitorSettings.Host;
        PortEntry.Text = MonitorSettings.Port.ToString();
        SlaveEntry.Text = MonitorSettings.SlaveId.ToString();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!int.TryParse(PortEntry.Text, out var port) || port is < 1 or > 65535 ||
            !int.TryParse(SlaveEntry.Text, out var slave) || slave is < 1 or > 255)
        {
            ResultLabel.TextColor = Color.FromArgb("#FF7B7B");
            ResultLabel.Text = "Port must be 1–65535 and slave ID 1–255.";
            return;
        }

        MonitorSettings.DemoMode = DemoSwitch.IsToggled;
        MonitorSettings.DeviceName = string.IsNullOrWhiteSpace(DeviceNameEntry.Text) ? "SolarHub" : DeviceNameEntry.Text.Trim();
        MonitorSettings.Host = HostEntry.Text?.Trim() ?? "";
        MonitorSettings.Port = port;
        MonitorSettings.SlaveId = slave;
        await DashboardViewModel.Current.RestartAsync();
        ResultLabel.TextColor = Color.FromArgb("#38D39F");
        ResultLabel.Text = "Saved. Monitor reconnected.";
    }
}

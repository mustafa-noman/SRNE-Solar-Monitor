namespace SolarPowerMonitor.Mobile.Services;

public static class MonitorSettings
{
    public static bool DemoMode { get => Preferences.Get(nameof(DemoMode), true); set => Preferences.Set(nameof(DemoMode), value); }
    public static string DeviceName { get => Preferences.Get(nameof(DeviceName), "SolarHub"); set => Preferences.Set(nameof(DeviceName), value); }
    public static string Host { get => Preferences.Get(nameof(Host), ""); set => Preferences.Set(nameof(Host), value); }
    public static int Port { get => Preferences.Get(nameof(Port), 8899); set => Preferences.Set(nameof(Port), value); }
    public static int SlaveId { get => Preferences.Get(nameof(SlaveId), 255); set => Preferences.Set(nameof(SlaveId), value); }
}

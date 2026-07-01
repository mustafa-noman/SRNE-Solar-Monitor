namespace SolarPowerMonitor.Mobile.Services;

public enum MonitorMode
{
    Cloud,
    Direct,
    Demo
}

public sealed record MonitorConfiguration(
    MonitorMode Mode,
    string DeviceName,
    string Host,
    int Port,
    int SlaveId,
    bool BackgroundEnabled,
    int BackgroundIntervalHours);

public static class MonitorSettings
{
    private const string DeviceIdKey = "srne-device-id";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool initialized;

    public static MonitorConfiguration Current { get; private set; } =
        new(MonitorMode.Cloud, "SolarHub", "", 8899, 255, true, 1);

    public static MonitorMode Mode => Current.Mode;
    public static string DeviceName => Current.DeviceName;
    public static string Host => Current.Host;
    public static int Port => Current.Port;
    public static int SlaveId => Current.SlaveId;
    public static bool BackgroundEnabled => Current.BackgroundEnabled;
    public static int BackgroundIntervalHours => Current.BackgroundIntervalHours;

    public static async Task InitializeAsync()
    {
        if (initialized) return;
        await Gate.WaitAsync();
        try
        {
            if (initialized) return;
            await SolarDatabase.InitializeAsync();

            var modeText = await SolarDatabase.GetSettingAsync("mode");
            if (modeText is null)
            {
                Current = new MonitorConfiguration(
                    Enum.TryParse<MonitorMode>(Preferences.Get("Mode", nameof(MonitorMode.Cloud)), out var migratedMode) ? migratedMode : MonitorMode.Cloud,
                    Preferences.Get("DeviceName", "SolarHub"),
                    Preferences.Get("Host", ""),
                    Preferences.Get("Port", 8899),
                    Preferences.Get("SlaveId", 255),
                    true,
                    1);
                await SaveCoreAsync(Current);
            }
            else
            {
                Current = new MonitorConfiguration(
                    Enum.TryParse<MonitorMode>(modeText, out var mode) ? mode : MonitorMode.Cloud,
                    await SolarDatabase.GetSettingAsync("device_name") ?? "SolarHub",
                    await SolarDatabase.GetSettingAsync("host") ?? "",
                    ParseInt(await SolarDatabase.GetSettingAsync("port"), 8899),
                    ParseInt(await SolarDatabase.GetSettingAsync("slave_id"), 255),
                    ParseBool(await SolarDatabase.GetSettingAsync("background_enabled"), true),
                    Math.Clamp(ParseInt(await SolarDatabase.GetSettingAsync("background_interval_hours"), 1), 1, 168));
            }
            initialized = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task SaveAsync(MonitorConfiguration configuration)
    {
        await InitializeAsync();
        await Gate.WaitAsync();
        try
        {
            await SaveCoreAsync(configuration);
            Current = configuration;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<string> GetDeviceIdAsync() =>
        (await SecureStorage.Default.GetAsync(DeviceIdKey) ?? "").Trim().ToUpperInvariant();

    public static Task SetDeviceIdAsync(string deviceId) =>
        SecureStorage.Default.SetAsync(DeviceIdKey, deviceId.Trim().ToUpperInvariant());

    private static Task SaveCoreAsync(MonitorConfiguration configuration) =>
        SolarDatabase.SaveSettingsAsync(new Dictionary<string, string>
        {
            ["mode"] = configuration.Mode.ToString(),
            ["device_name"] = configuration.DeviceName,
            ["host"] = configuration.Host,
            ["port"] = configuration.Port.ToString(),
            ["slave_id"] = configuration.SlaveId.ToString(),
            ["background_enabled"] = configuration.BackgroundEnabled.ToString(),
            ["background_interval_hours"] = configuration.BackgroundIntervalHours.ToString()
        });

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool ParseBool(string? value, bool fallback) =>
        bool.TryParse(value, out var parsed) ? parsed : fallback;
}

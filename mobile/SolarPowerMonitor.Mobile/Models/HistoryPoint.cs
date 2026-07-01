namespace SolarPowerMonitor.Mobile.Models;

public sealed record HistoryPoint(DateTime Timestamp, ushort SolarWatts, ushort BatteryPercent)
{
    public string TimeText => Timestamp.ToString("HH:mm:ss");
    public string SolarText => $"{SolarWatts} W";
    public string BatteryText => $"{BatteryPercent}%";
}

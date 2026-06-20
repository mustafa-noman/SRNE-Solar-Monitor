namespace SolarPowerMonitor;

internal sealed record SolarTelemetry(
    ushort BatteryStateOfCharge,
    decimal PvArrayVoltage,
    decimal PvArrayCurrent,
    ushort PvChargingPower,
    decimal BatteryVoltage,
    decimal BatteryChargingCurrent)
{
    public static SolarTelemetry FromRegisters(IReadOnlyList<ushort> registers)
    {
        if (registers.Count < 10)
        {
            throw new ArgumentException(
                $"Expected at least 10 registers but received {registers.Count}.",
                nameof(registers));
        }

        return new SolarTelemetry(
            registers[0],
            registers[7] / 10m,
            registers[8] / 100m,
            registers[9],
            registers[1] / 10m,
            registers[2] / 100m);
    }
}

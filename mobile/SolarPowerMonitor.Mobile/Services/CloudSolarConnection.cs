using SolarPowerMonitor.Mobile.Models;

namespace SolarPowerMonitor.Mobile.Services;

public sealed class CloudSolarConnection : IAsyncDisposable
{
    private SolarPowerMonitor.SrneCloudClient? client;
    private string connectedDeviceId = "";

    public async Task<SolarTelemetry> ReadAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (client is null || !string.Equals(connectedDeviceId, deviceId, StringComparison.Ordinal))
        {
            await ResetAsync();
            client = new SolarPowerMonitor.SrneCloudClient(
                deviceId,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15));
            await client.ConnectAsync(cancellationToken);
            connectedDeviceId = deviceId;
        }

        try
        {
            var registers = await client.ReadTelemetryAsync(cancellationToken);
            return SolarTelemetry.FromRegisters(registers);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
    }

    public async ValueTask ResetAsync()
    {
        if (client is not null)
            await client.DisposeAsync();
        client = null;
        connectedDeviceId = "";
    }

    public ValueTask DisposeAsync() => ResetAsync();
}

using Android.App;
using Android.App.Job;
using Android.Content;
using SolarPowerMonitor.Mobile.Models;

namespace SolarPowerMonitor.Mobile.Services;

[Service(
    Name = "com.mustafanoman.solarmonitor.TelemetryJobService",
    Permission = "android.permission.BIND_JOB_SERVICE",
    Exported = false)]
public sealed class TelemetryJobService : JobService
{
    private CancellationTokenSource? cancellation;

    public override bool OnStartJob(JobParameters? parameters)
    {
        cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        _ = CollectAsync(parameters, cancellation.Token);
        return true;
    }

    public override bool OnStopJob(JobParameters? parameters)
    {
        cancellation?.Cancel();
        return true;
    }

    private async Task CollectAsync(JobParameters? parameters, CancellationToken cancellationToken)
    {
        var shouldRetry = false;
        try
        {
            await MonitorSettings.InitializeAsync();
            var telemetry = await ReadOnceAsync(cancellationToken);
            if (telemetry is not null)
            {
                await SolarDatabase.InsertTelemetryAsync(
                    telemetry,
                    MonitorSettings.Mode.ToString(),
                    "background",
                    DateTimeOffset.Now);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            shouldRetry = true;
        }
        catch
        {
            shouldRetry = true;
        }
        finally
        {
            JobFinished(parameters, shouldRetry);
            cancellation?.Dispose();
            cancellation = null;
        }
    }

    private static async Task<SolarTelemetry?> ReadOnceAsync(CancellationToken cancellationToken)
    {
        if (MonitorSettings.Mode == MonitorMode.Demo)
            return null;

        if (MonitorSettings.Mode == MonitorMode.Direct)
        {
            if (string.IsNullOrWhiteSpace(MonitorSettings.Host))
                throw new InvalidOperationException("Direct controller host is not configured.");
            return await ModbusSolarClient.ReadAsync(
                MonitorSettings.Host,
                MonitorSettings.Port,
                checked((byte)MonitorSettings.SlaveId),
                cancellationToken);
        }

        var deviceId = await MonitorSettings.GetDeviceIdAsync();
        if (deviceId.Length != 8 || !deviceId.All(Uri.IsHexDigit))
            throw new InvalidOperationException("SRNE device ID is not configured.");
        await using var cloud = new CloudSolarConnection();
        return await cloud.ReadAsync(deviceId, cancellationToken);
    }
}

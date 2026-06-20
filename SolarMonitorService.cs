using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace SolarPowerMonitor;

internal sealed class SolarMonitorService(AppOptions options, ConsoleDashboard dashboard)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (options.Source == MonitorSource.Cloud)
        {
            await RunCloudAsync(cancellationToken);
            dashboard.RenderStopped();
            return;
        }

        await RunDirectAsync(cancellationToken);
        dashboard.RenderStopped();
    }

    private async Task RunCloudAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            dashboard.RenderConnecting(options);

            await using var client = new SrneCloudClient(
                options.DeviceId,
                options.ConnectTimeout,
                options.ResponseTimeout);

            try
            {
                await client.ConnectAsync(cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var registers = await client.ReadTelemetryAsync(cancellationToken);
                    stopwatch.Stop();

                    dashboard.RenderConnected(
                        options,
                        SolarTelemetry.FromRegisters(registers),
                        stopwatch.Elapsed);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                dashboard.RenderDisconnected(options, Describe(exception));
                await Task.Delay(options.ReconnectDelay, cancellationToken);
            }
        }
    }

    private async Task RunDirectAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            dashboard.RenderConnecting(options);

            await using var client = new ModbusRtuTcpClient(
                options.Host,
                options.Port,
                options.SlaveId,
                options.ConnectTimeout,
                options.ResponseTimeout);

            try
            {
                await client.ConnectAsync(cancellationToken);
                await PollConnectedDeviceAsync(client, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                dashboard.RenderDisconnected(options, Describe(exception));
                await Task.Delay(options.ReconnectDelay, cancellationToken);
            }
        }
    }

    private async Task PollConnectedDeviceAsync(
        ModbusRtuTcpClient client,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(options.PollInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            var stopwatch = Stopwatch.StartNew();
            var registers = await client.ReadHoldingRegistersAsync(
                AppOptions.StartRegister,
                AppOptions.RegisterCount,
                cancellationToken);
            stopwatch.Stop();

            var telemetry = SolarTelemetry.FromRegisters(registers);
            dashboard.RenderConnected(options, telemetry, stopwatch.Elapsed);

            if (!await timer.WaitForNextTickAsync(cancellationToken))
            {
                break;
            }
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is SocketException
            or IOException
            or TimeoutException
            or InvalidOperationException;

    private static string Describe(Exception exception) =>
        exception switch
        {
            SocketException socketException =>
                $"{socketException.SocketErrorCode}: {socketException.Message}",
            TimeoutException =>
                exception.Message,
            _ => exception.Message
        };
}

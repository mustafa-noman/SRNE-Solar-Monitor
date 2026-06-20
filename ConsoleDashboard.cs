using System.IO;

namespace SolarPowerMonitor;

internal sealed class ConsoleDashboard
{
    private readonly object _consoleLock = new();

    public void RenderConnecting(AppOptions options)
    {
        Render(() =>
        {
            WriteHeader();
            WriteStatus("CONNECTING", ConsoleColor.Yellow);
            WriteTarget(options);
            Console.WriteLine($"Timestamp       {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine();
            Console.WriteLine(
                options.Source == MonitorSource.Cloud
                    ? "Connecting to the SRNE live telemetry feed..."
                    : "Opening transparent Modbus RTU TCP stream...");
            WriteFooter();
        });
    }

    public void RenderConnected(AppOptions options, SolarTelemetry telemetry, TimeSpan latency)
    {
        Render(() =>
        {
            WriteHeader();
            WriteStatus("ONLINE", ConsoleColor.Green);
            WriteTarget(options);
            Console.WriteLine($"Timestamp       {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine($"Update wait     {latency.TotalMilliseconds,7:0.0} ms");
            Console.WriteLine();
            Console.WriteLine("  SOLAR ARRAY");
            Console.WriteLine($"  Voltage              {telemetry.PvArrayVoltage,10:0.0} V");
            Console.WriteLine($"  Current              {telemetry.PvArrayCurrent,10:0.00} A");
            Console.WriteLine($"  Charging power       {telemetry.PvChargingPower,10} W");
            Console.WriteLine();
            Console.WriteLine("  BATTERY");
            Console.WriteLine($"  State of charge       {telemetry.BatteryStateOfCharge,10} %");
            Console.WriteLine($"  Voltage              {telemetry.BatteryVoltage,10:0.0} V");
            Console.WriteLine($"  Charging current     {telemetry.BatteryChargingCurrent,10:0.00} A");
            WriteFooter();
        });
    }

    public void RenderDisconnected(AppOptions options, string reason)
    {
        Render(() =>
        {
            WriteHeader();
            WriteStatus("OFFLINE", ConsoleColor.Red);
            WriteTarget(options);
            Console.WriteLine($"Timestamp       {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine($"Last error      {reason}");
            Console.WriteLine();
            Console.WriteLine(
                $"Reconnecting in {options.ReconnectDelay.TotalSeconds:0.###} seconds...");
            WriteFooter();
        });
    }

    public void RenderStopped()
    {
        Render(() =>
        {
            WriteHeader();
            WriteStatus("STOPPED", ConsoleColor.DarkYellow);
            Console.WriteLine($"Timestamp       {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine("Monitoring stopped cleanly.");
        });
    }

    public void RenderFatal(string message)
    {
        Render(() =>
        {
            WriteHeader();
            WriteStatus("FATAL", ConsoleColor.Red);
            Console.WriteLine(message);
        });
    }

    private void Render(Action renderContent)
    {
        lock (_consoleLock)
        {
            TryClear();
            renderContent();
        }
    }

    private static void WriteHeader()
    {
        Console.WriteLine("============================================================");
        Console.WriteLine(" SRNE SHINER2440 SOLAR TELEMETRY MONITOR");
        Console.WriteLine(" Live telemetry from the SRNE Wi-Fi module");
        Console.WriteLine("============================================================");
        Console.WriteLine();
    }

    private static void WriteStatus(string status, ConsoleColor color)
    {
        Console.Write("Status          ");

        if (!Console.IsOutputRedirected)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(status);
            Console.ForegroundColor = previousColor;
        }
        else
        {
            Console.WriteLine(status);
        }
    }

    private static void WriteFooter()
    {
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop.");
    }

    private static void WriteTarget(AppOptions options)
    {
        if (options.Source == MonitorSource.Cloud)
        {
            Console.WriteLine($"Source          SRNE cloud  |  Device {options.DeviceId}");
        }
        else
        {
            Console.WriteLine(
                $"Target          {options.Host}:{options.Port}  |  Slave {options.SlaveId}");
        }
    }

    private static void TryClear()
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            // Some terminals do not support clearing; the dashboard can still render.
        }
    }
}

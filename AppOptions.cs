using System.Globalization;
using System.Net;

namespace SolarPowerMonitor;

internal sealed record AppOptions(
    MonitorSource Source,
    string DeviceId,
    string Host,
    int Port,
    byte SlaveId,
    TimeSpan PollInterval,
    TimeSpan ConnectTimeout,
    TimeSpan ResponseTimeout,
    TimeSpan ReconnectDelay)
{
    public const ushort StartRegister = 0x0100;
    public const ushort RegisterCount = 35;

    public static AppOptions Parse(string[] args)
    {
        var values = ParseArguments(args);

        var source = ParseSource(Get(values, "source", "SOLAR_SOURCE", "cloud"));
        var deviceId = Get(values, "device-id", "SOLAR_DEVICE_ID", "YOUR_DEVICE_ID")
            .Trim()
            .ToUpperInvariant();
        var host = Get(values, "host", "SOLAR_HOST", "192.168.10.167");
        var port = ParseInt(Get(values, "port", "SOLAR_PORT", "8899"), "port", 1, 65535);
        var slaveId = ParseInt(Get(values, "slave", "SOLAR_SLAVE_ID", "255"), "slave", 1, 255);
        var pollMs = ParseInt(Get(values, "poll-ms", "SOLAR_POLL_MS", "2000"), "poll-ms", 100, 3_600_000);
        var connectTimeoutMs = ParseInt(
            Get(values, "connect-timeout-ms", "SOLAR_CONNECT_TIMEOUT_MS", "5000"),
            "connect-timeout-ms",
            100,
            120_000);
        var responseTimeoutMs = ParseInt(
            Get(values, "response-timeout-ms", "SOLAR_RESPONSE_TIMEOUT_MS", "15000"),
            "response-timeout-ms",
            100,
            120_000);
        var reconnectDelayMs = ParseInt(
            Get(values, "reconnect-delay-ms", "SOLAR_RECONNECT_DELAY_MS", "2000"),
            "reconnect-delay-ms",
            100,
            3_600_000);

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host cannot be empty.");
        }

        if (deviceId.Length != 8 || !deviceId.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "'device-id' must be the eight hexadecimal characters shown in the SRNE app.");
        }

        if (!IPAddress.TryParse(host, out _) && Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            throw new ArgumentException($"'{host}' is not a valid IP address or host name.");
        }

        return new AppOptions(
            source,
            deviceId,
            host,
            port,
            checked((byte)slaveId),
            TimeSpan.FromMilliseconds(pollMs),
            TimeSpan.FromMilliseconds(connectTimeoutMs),
            TimeSpan.FromMilliseconds(responseTimeoutMs),
            TimeSpan.FromMilliseconds(reconnectDelayMs));
    }

    public static void PrintUsage()
    {
        Console.WriteLine(
            """
            Usage: dotnet run -- [options]

              --source <cloud|direct>        Data source (default cloud)
              --device-id <value>            SRNE Wi-Fi device ID (default YOUR_DEVICE_ID)
              --host <value>                 Bridge IP/host (default 192.168.10.167)
              --port <value>                 Bridge TCP port (default 8899)
              --slave <value>                Direct-mode Modbus slave ID (default 255)
              --poll-ms <value>              Poll interval (default 2000)
              --connect-timeout-ms <value>   Connection timeout (default 5000)
              --response-timeout-ms <value>  Telemetry timeout (default 15000)
              --reconnect-delay-ms <value>   Delay before reconnect (default 2000)
              --self-test                    Run protocol tests and exit

            The same settings may be supplied with SOLAR_SOURCE, SOLAR_DEVICE_ID,
            SOLAR_HOST, SOLAR_PORT, SOLAR_SLAVE_ID, SOLAR_POLL_MS,
            SOLAR_CONNECT_TIMEOUT_MS, SOLAR_RESPONSE_TIMEOUT_MS, and
            SOLAR_RECONNECT_DELAY_MS.
            """);
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument is "--help" or "-h")
            {
                PrintUsage();
                Environment.Exit(0);
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{argument}'.");
            }

            var name = argument[2..];
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for '--{name}'.");
            }

            values[name] = args[++index];
        }

        return values;
    }

    private static string Get(
        IReadOnlyDictionary<string, string> arguments,
        string argumentName,
        string environmentName,
        string defaultValue)
    {
        if (arguments.TryGetValue(argumentName, out var argumentValue))
        {
            return argumentValue;
        }

        return Environment.GetEnvironmentVariable(environmentName) ?? defaultValue;
    }

    private static int ParseInt(string value, string name, int minimum, int maximum)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new ArgumentException(
                $"'{name}' must be an integer between {minimum} and {maximum}.");
        }

        return parsed;
    }

    private static MonitorSource ParseSource(string value) =>
        value.ToLowerInvariant() switch
        {
            "cloud" => MonitorSource.Cloud,
            "direct" => MonitorSource.Direct,
            _ => throw new ArgumentException("'source' must be 'cloud' or 'direct'.")
        };
}

internal enum MonitorSource
{
    Cloud,
    Direct
}

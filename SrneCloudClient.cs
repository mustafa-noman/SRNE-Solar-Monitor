using System.Buffers.Binary;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SolarPowerMonitor;

internal sealed class SrneCloudClient : IAsyncDisposable
{
    private const ushort RegisterCount = 35;
    private const string DeviceLookupUrl =
        "http://www.srne.net:9006/api/mqtt/getclients/?clientid=WIFI-";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly string _deviceId;
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _responseTimeout;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private string _topic = "";

    public SrneCloudClient(
        string deviceId,
        TimeSpan connectTimeout,
        TimeSpan responseTimeout)
    {
        _deviceId = deviceId;
        _connectTimeout = connectTimeout;
        _responseTimeout = responseTimeout;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);

        var client = new TcpClient { NoDelay = true };

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_connectTimeout);
            await client.ConnectAsync(configuration.Broker, configuration.Port, timeout.Token);

            _tcpClient = client;
            _stream = client.GetStream();
            _topic = configuration.PublishTopic;

            var clientId = $"SolarPowerMonitor-{Guid.NewGuid():N}"[..32];
            await WritePacketAsync(
                0x10,
                BuildConnectPayload(
                    clientId,
                    configuration.Username,
                    configuration.Password),
                cancellationToken);

            var connAck = await ReadPacketAsync(cancellationToken);
            if (connAck.Header != 0x20 ||
                connAck.Payload.Length != 2 ||
                connAck.Payload[1] != 0)
            {
                throw new IOException("The SRNE MQTT broker rejected the connection.");
            }

            await WritePacketAsync(0x82, BuildSubscribePayload(_topic), cancellationToken);

            var subAck = await ReadPacketAsync(cancellationToken);
            if (subAck.Header != 0x90)
            {
                throw new IOException("The SRNE MQTT broker rejected the telemetry subscription.");
            }
        }
        catch
        {
            client.Dispose();
            _tcpClient = null;
            _stream = null;
            throw;
        }
    }

    public async Task<ushort[]> ReadTelemetryAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var packet = await ReadPacketAsync(cancellationToken);
            var packetType = packet.Header & 0xF0;

            if (packetType == 0x30)
            {
                var (topic, payload) = ParsePublishPacket(packet);
                if (topic == _topic &&
                    TryExtractRegisterFrame(payload, out var frame))
                {
                    return ModbusRtuTcpClient.ParseReadHoldingRegistersResponse(
                        frame,
                        255,
                        RegisterCount);
                }
            }
            else if (packetType == 0xD0)
            {
                continue;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _stream = null;
        _tcpClient = null;
        return ValueTask.CompletedTask;
    }

    private async Task<CloudConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(
            DeviceLookupUrl + Uri.EscapeDataString(_deviceId),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            body,
            cancellationToken: cancellationToken);

        var devices = document.RootElement
            .GetProperty("data")
            .GetProperty("data");

        if (devices.GetArrayLength() == 0)
        {
            throw new IOException($"SRNE device {_deviceId} was not found.");
        }

        var device = devices[0];
        if (device.GetProperty("connectstatus").GetString() != "1")
        {
            throw new IOException($"SRNE device {_deviceId} is offline.");
        }

        var configText = device.GetProperty("configinfo").GetString();
        if (string.IsNullOrWhiteSpace(configText))
        {
            throw new IOException("SRNE did not return the device MQTT configuration.");
        }

        using var config = JsonDocument.Parse(configText);
        var mqtt = config.RootElement.GetProperty("mqtt");

        return new CloudConfiguration(
            mqtt.GetProperty("mqtt_broker").GetString()
                ?? throw new IOException("SRNE MQTT broker is missing."),
            int.Parse(
                mqtt.GetProperty("mqtt_port").GetString()
                    ?? throw new IOException("SRNE MQTT port is missing.")),
            mqtt.GetProperty("mqtt_user").GetString() ?? "",
            mqtt.GetProperty("mqtt_pwd").GetString() ?? "",
            mqtt.GetProperty("mqtt_pubtopic").GetString()
                ?? throw new IOException("SRNE MQTT publish topic is missing."));
    }

    private async Task<MqttPacket> ReadPacketAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("The SRNE cloud connection is not open.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_responseTimeout);

        try
        {
            var header = await ReadByteAsync(_stream, timeout.Token);
            var remainingLength = await ReadRemainingLengthAsync(_stream, timeout.Token);
            var payload = new byte[remainingLength];
            await ReadExactlyAsync(_stream, payload, timeout.Token);
            return new MqttPacket(header, payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No SRNE cloud telemetry was received within " +
                $"{_responseTimeout.TotalMilliseconds:0} ms.");
        }
    }

    private async Task WritePacketAsync(
        byte header,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("The SRNE cloud connection is not open.");
        }

        var remainingLength = EncodeRemainingLength(payload.Length);
        var packet = new byte[1 + remainingLength.Length + payload.Length];
        packet[0] = header;
        remainingLength.CopyTo(packet, 1);
        payload.CopyTo(packet, 1 + remainingLength.Length);

        await _stream.WriteAsync(packet, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    private static byte[] BuildConnectPayload(
        string clientId,
        string username,
        string password)
    {
        using var stream = new MemoryStream();
        WriteMqttString(stream, "MQTT");
        stream.WriteByte(4);
        stream.WriteByte(0xC2);
        stream.WriteByte(0);
        stream.WriteByte(30);
        WriteMqttString(stream, clientId);
        WriteMqttString(stream, username);
        WriteMqttString(stream, password);
        return stream.ToArray();
    }

    private static byte[] BuildSubscribePayload(string topic)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0);
        stream.WriteByte(1);
        WriteMqttString(stream, topic);
        stream.WriteByte(0);
        return stream.ToArray();
    }

    private static (string Topic, ReadOnlyMemory<byte> Payload) ParsePublishPacket(
        MqttPacket packet)
    {
        if (packet.Payload.Length < 2)
        {
            throw new IOException("SRNE sent an invalid MQTT publish packet.");
        }

        var topicLength = BinaryPrimitives.ReadUInt16BigEndian(packet.Payload);
        var offset = 2;

        if (packet.Payload.Length < offset + topicLength)
        {
            throw new IOException("SRNE sent a truncated MQTT topic.");
        }

        var topic = Encoding.UTF8.GetString(packet.Payload, offset, topicLength);
        offset += topicLength;

        var qos = (packet.Header >> 1) & 0x03;
        if (qos > 0)
        {
            offset += 2;
        }

        if (offset > packet.Payload.Length)
        {
            throw new IOException("SRNE sent a truncated MQTT publish packet.");
        }

        return (topic, packet.Payload.AsMemory(offset));
    }

    private static bool TryExtractRegisterFrame(
        ReadOnlyMemory<byte> payload,
        out byte[] frame)
    {
        frame = [];

        try
        {
            using var document = JsonDocument.Parse(payload);
            foreach (var deviceData in document.RootElement.GetProperty("data").EnumerateArray())
            {
                foreach (var modbus in deviceData.GetProperty("modbus").EnumerateArray())
                {
                    if (modbus.GetProperty("startaddress").GetString() != "0100")
                    {
                        continue;
                    }

                    var hex = modbus.GetProperty("returndata").GetString();
                    if (string.IsNullOrWhiteSpace(hex))
                    {
                        return false;
                    }

                    frame = Convert.FromHexString(hex);
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }

        return false;
    }

    private static void WriteMqttString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new ArgumentException("MQTT string is too long.", nameof(value));
        }

        stream.WriteByte((byte)(bytes.Length >> 8));
        stream.WriteByte((byte)bytes.Length);
        stream.Write(bytes);
    }

    private static byte[] EncodeRemainingLength(int length)
    {
        var bytes = new List<byte>(4);
        do
        {
            var encoded = (byte)(length % 128);
            length /= 128;
            if (length > 0)
            {
                encoded |= 0x80;
            }

            bytes.Add(encoded);
        }
        while (length > 0);

        return [.. bytes];
    }

    private static async Task<int> ReadRemainingLengthAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var multiplier = 1;
        var value = 0;

        for (var index = 0; index < 4; index++)
        {
            var encoded = await ReadByteAsync(stream, cancellationToken);
            value += (encoded & 127) * multiplier;
            if ((encoded & 128) == 0)
            {
                return value;
            }

            multiplier *= 128;
        }

        throw new IOException("Invalid MQTT remaining length.");
    }

    private static async Task<byte> ReadByteAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        await ReadExactlyAsync(stream, buffer, cancellationToken);
        return buffer[0];
    }

    private static async Task ReadExactlyAsync(
        NetworkStream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (count == 0)
            {
                throw new IOException("The SRNE cloud connection was closed.");
            }

            offset += count;
        }
    }

    private sealed record CloudConfiguration(
        string Broker,
        int Port,
        string Username,
        string Password,
        string PublishTopic);

    private sealed record MqttPacket(byte Header, byte[] Payload);
}

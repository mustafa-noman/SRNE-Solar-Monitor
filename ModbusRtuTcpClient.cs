using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;

namespace SolarPowerMonitor;

internal sealed class ModbusRtuTcpClient : IAsyncDisposable
{
    private const byte ReadHoldingRegistersFunction = 0x03;

    private readonly string _host;
    private readonly int _port;
    private readonly byte _slaveId;
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _responseTimeout;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;

    public ModbusRtuTcpClient(
        string host,
        int port,
        byte slaveId,
        TimeSpan connectTimeout,
        TimeSpan responseTimeout)
    {
        _host = host;
        _port = port;
        _slaveId = slaveId;
        _connectTimeout = connectTimeout;
        _responseTimeout = responseTimeout;
    }

    public bool IsConnected => _tcpClient?.Connected == true && _stream is not null;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await DisposeConnectionAsync();

        var client = new TcpClient
        {
            NoDelay = true,
            ReceiveBufferSize = 256,
            SendBufferSize = 256
        };

        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_connectTimeout);

            await client.ConnectAsync(_host, _port, timeout.Token);
            _tcpClient = client;
            _stream = client.GetStream();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            throw new TimeoutException(
                $"Connection to {_host}:{_port} timed out after {_connectTimeout.TotalMilliseconds:0} ms.");
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(
        ushort startAddress,
        ushort registerCount,
        CancellationToken cancellationToken)
    {
        if (_stream is null || !IsConnected)
        {
            throw new InvalidOperationException("The Modbus connection is not open.");
        }

        var request = BuildReadRequest(_slaveId, startAddress, registerCount);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_responseTimeout);

        try
        {
            await _stream.WriteAsync(request, timeout.Token);
            await _stream.FlushAsync(timeout.Token);

            var header = new byte[3];
            await ReadExactlyAsync(_stream, header, timeout.Token);

            var remainingLength = (header[1] & 0x80) != 0
                ? 2
                : checked(header[2] + 2);

            var frame = new byte[header.Length + remainingLength];
            header.CopyTo(frame, 0);
            await ReadExactlyAsync(_stream, frame.AsMemory(header.Length, remainingLength), timeout.Token);

            return ParseReadHoldingRegistersResponse(frame, _slaveId, registerCount);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No complete Modbus response was received within {_responseTimeout.TotalMilliseconds:0} ms.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionAsync();
        GC.SuppressFinalize(this);
    }

    internal static byte[] BuildReadRequest(byte slaveId, ushort startAddress, ushort registerCount)
    {
        var frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = ReadHoldingRegistersFunction;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4, 2), registerCount);

        var crc = ComputeCrc16(frame.AsSpan(0, 6));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6, 2), crc);
        return frame;
    }

    internal static ushort[] ParseReadHoldingRegistersResponse(
        ReadOnlySpan<byte> frame,
        byte expectedSlaveId,
        ushort expectedRegisterCount)
    {
        if (frame.Length < 5)
        {
            throw new ModbusProtocolException("The Modbus response is too short.");
        }

        var suppliedCrc = BinaryPrimitives.ReadUInt16LittleEndian(frame[^2..]);
        var calculatedCrc = ComputeCrc16(frame[..^2]);
        if (suppliedCrc != calculatedCrc)
        {
            throw new ModbusProtocolException(
                $"CRC mismatch. Received 0x{suppliedCrc:X4}, calculated 0x{calculatedCrc:X4}.");
        }

        if (frame[0] != expectedSlaveId)
        {
            throw new ModbusProtocolException(
                $"Unexpected slave ID {frame[0]}; expected {expectedSlaveId}.");
        }

        if ((frame[1] & 0x80) != 0)
        {
            throw new ModbusProtocolException(
                $"Device returned Modbus exception code 0x{frame[2]:X2}.");
        }

        if (frame[1] != ReadHoldingRegistersFunction)
        {
            throw new ModbusProtocolException(
                $"Unexpected function code 0x{frame[1]:X2}; expected 0x03.");
        }

        var expectedByteCount = checked(expectedRegisterCount * 2);
        if (frame[2] != expectedByteCount)
        {
            throw new ModbusProtocolException(
                $"Unexpected payload length {frame[2]}; expected {expectedByteCount} bytes.");
        }

        if (frame.Length != 3 + expectedByteCount + 2)
        {
            throw new ModbusProtocolException(
                $"Unexpected frame length {frame.Length}; expected {3 + expectedByteCount + 2} bytes.");
        }

        var registers = new ushort[expectedRegisterCount];
        for (var index = 0; index < registers.Length; index++)
        {
            registers[index] = BinaryPrimitives.ReadUInt16BigEndian(
                frame.Slice(3 + (index * 2), 2));
        }

        return registers;
    }

    internal static ushort ComputeCrc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;

        foreach (var value in data)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x0001) != 0
                    ? (ushort)((crc >> 1) ^ 0xA001)
                    : (ushort)(crc >> 1);
            }
        }

        return crc;
    }

    private static async Task ReadExactlyAsync(
        NetworkStream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (bytesRead == 0)
            {
                throw new IOException("The bridge closed the TCP connection.");
            }

            offset += bytesRead;
        }
    }

    private ValueTask DisposeConnectionAsync()
    {
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _stream = null;
        _tcpClient = null;
        return ValueTask.CompletedTask;
    }
}

internal sealed class ModbusProtocolException(string message) : IOException(message);

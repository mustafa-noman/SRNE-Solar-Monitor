using System.Buffers.Binary;
using System.Net.Sockets;
using SolarPowerMonitor.Mobile.Models;

namespace SolarPowerMonitor.Mobile.Services;

public static class ModbusSolarClient
{
    public static async Task<SolarTelemetry> ReadAsync(string host, int port, byte slaveId, CancellationToken cancellationToken)
    {
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(host, port, cancellationToken);
        await using var stream = client.GetStream();
        var request = BuildRequest(slaveId, 0x0100, 35);
        await stream.WriteAsync(request, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        var header = new byte[3];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var frame = new byte[5 + header[2]];
        header.CopyTo(frame, 0);
        await ReadExactlyAsync(stream, frame.AsMemory(3), cancellationToken);
        ValidateFrame(frame, slaveId, 35);

        var registers = new ushort[35];
        for (var i = 0; i < registers.Length; i++)
            registers[i] = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(3 + i * 2, 2));
        return SolarTelemetry.FromRegisters(registers);
    }

    internal static byte[] BuildRequest(byte slaveId, ushort start, ushort count)
    {
        var frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), start);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4, 2), count);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6, 2), ComputeCrc(frame.AsSpan(0, 6)));
        return frame;
    }

    private static void ValidateFrame(byte[] frame, byte slaveId, ushort count)
    {
        if (frame.Length != 5 + count * 2 || frame[0] != slaveId || frame[1] != 0x03 || frame[2] != count * 2)
            throw new IOException("Controller returned an invalid Modbus response.");
        var expected = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(frame.Length - 2));
        if (ComputeCrc(frame.AsSpan(0, frame.Length - 2)) != expected)
            throw new IOException("Controller response failed CRC validation.");
    }

    internal static ushort ComputeCrc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (ushort)(((crc & 1) != 0) ? (crc >> 1) ^ 0xA001 : crc >> 1);
        }
        return crc;
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (count == 0) throw new EndOfStreamException("Controller closed the connection.");
            offset += count;
        }
    }
}

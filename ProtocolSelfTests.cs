using System.Buffers.Binary;

namespace SolarPowerMonitor;

internal static class ProtocolSelfTests
{
    public static int Run()
    {
        try
        {
            VerifyReadRequest();
            VerifyResponseParsingAndScaling();
            VerifyCorruptResponseIsRejected();
            Console.WriteLine("All protocol self-tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Self-test failed: {exception.Message}");
            return 1;
        }
    }

    private static void VerifyReadRequest()
    {
        var request = ModbusRtuTcpClient.BuildReadRequest(255, 0x0100, 35);
        var expected = new byte[] { 0xFF, 0x03, 0x01, 0x00, 0x00, 0x23, 0x10, 0x31 };
        Assert(request.SequenceEqual(expected), "Read request frame or CRC is incorrect.");
    }

    private static void VerifyResponseParsingAndScaling()
    {
        var rawValues = new ushort[35];
        ushort[] telemetryValues = [85, 128, 345, 0, 128, 0, 0, 381, 661, 252];
        telemetryValues.CopyTo(rawValues, 0);
        var response = BuildResponse(255, rawValues);
        var parsed = ModbusRtuTcpClient.ParseReadHoldingRegistersResponse(response, 255, 35);
        var telemetry = SolarTelemetry.FromRegisters(parsed);

        Assert(telemetry.BatteryStateOfCharge == 85, "Battery SOC parsing is incorrect.");
        Assert(telemetry.PvArrayVoltage == 38.1m, "PV voltage scaling is incorrect.");
        Assert(telemetry.PvArrayCurrent == 6.61m, "PV current scaling is incorrect.");
        Assert(telemetry.PvChargingPower == 252, "PV power parsing is incorrect.");
        Assert(telemetry.BatteryVoltage == 12.8m, "Battery voltage scaling is incorrect.");
        Assert(
            telemetry.BatteryChargingCurrent == 3.45m,
            "Battery current scaling is incorrect.");
    }

    private static void VerifyCorruptResponseIsRejected()
    {
        var rawValues = new ushort[35];
        ushort[] telemetryValues = [85, 128, 345, 0, 128, 0, 0, 381, 661, 252];
        telemetryValues.CopyTo(rawValues, 0);
        var response = BuildResponse(255, rawValues);
        response[4] ^= 0x01;

        try
        {
            _ = ModbusRtuTcpClient.ParseReadHoldingRegistersResponse(response, 1, 5);
        }
        catch (ModbusProtocolException)
        {
            return;
        }

        throw new InvalidOperationException("A response with a bad CRC was accepted.");
    }

    private static byte[] BuildResponse(byte slaveId, IReadOnlyList<ushort> registers)
    {
        var response = new byte[3 + (registers.Count * 2) + 2];
        response[0] = slaveId;
        response[1] = 0x03;
        response[2] = checked((byte)(registers.Count * 2));

        for (var index = 0; index < registers.Count; index++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(
                response.AsSpan(3 + (index * 2), 2),
                registers[index]);
        }

        var crc = ModbusRtuTcpClient.ComputeCrc16(response.AsSpan(0, response.Length - 2));
        BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(response.Length - 2), crc);
        return response;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

# SRNE Shiner2440 Solar Telemetry Monitor

A dependency-free .NET 9 WPF desktop dashboard for an SRNE Shiner2440 MPPT
controller. It displays live solar generation, battery state, charging
current, and recent output history using the same SRNE MQTT telemetry feed as
the official mobile app.

## Protocol

- Default source: SRNE cloud/MQTT
- Default device ID: `YOUR_DEVICE_ID`
- Device upload protocol: Modbus RTU frames carried in SRNE MQTT messages
- Slave ID: `255`
- Register block: `0x0100` through `0x0122`

The implementation validates the slave ID, function code, response length,
Modbus exception responses, and CRC-16 on every response.

## Run

```powershell
dotnet run
```

Close the dashboard window to stop it.

### Desktop widget

Launch the compact always-on-top widget with:

```powershell
dotnet run -- --widget
```

The widget can be dragged anywhere on screen and includes a button to open
the full dashboard.

This project disables the generated native app-host executable so it can run
on Windows systems that block unsigned executables. Use `dotnet run`, or run
the built application with:

```powershell
dotnet .\bin\Debug\net9.0\SolarPowerMonitor.dll
```

To publish a standalone Windows executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:UseAppHost=true
```

The executable will be under
`bin\Release\net9.0\win-x64\publish\SolarPowerMonitor.exe`.
That executable must be code-signed or explicitly allowed by your
organization's Application Control policy before it can run on a restricted
PC.

## Configuration

Defaults match the connected controller:

```powershell
dotnet run -- --source cloud --device-id YOUR_DEVICE_ID
```

Available options:

- `--source`
- `--device-id`
- `--host`
- `--port`
- `--slave`
- `--poll-ms`
- `--connect-timeout-ms`
- `--response-timeout-ms`
- `--reconnect-delay-ms`

Equivalent environment variables begin with `SOLAR_`.

The original transparent TCP mode remains available:

```powershell
dotnet run -- --source direct --host 192.168.10.167 --port 8899 --slave 255
```

## Offline protocol verification

```powershell
dotnet run -- --self-test
```

This checks the exact request bytes and CRC, register parsing and scale
factors, and rejection of corrupted responses without requiring the physical
controller.

## Bridge setup notes

The HF-LPB170 must be configured as a transparent TCP server (or compatible
socket endpoint) using the same serial settings as the controller. Modbus RTU
timing and serial parameters are handled by the bridge; this application sends
complete RTU request frames and expects complete RTU response frames.

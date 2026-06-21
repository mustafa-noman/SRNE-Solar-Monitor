# SRNE Solar Monitor

A modern Windows desktop dashboard and always-on-top widget for monitoring an
SRNE Shiner-series MPPT solar charge controller.

The application displays live:

- Solar-panel power, voltage, and current
- Battery state of charge, voltage, and charging current
- Connection status and last update time
- Recent solar-output history
- A compact, draggable desktop widget

The default connection uses the SRNE cloud MQTT telemetry feed. A direct
Modbus RTU-over-TCP mode is also available for compatible Wi-Fi/serial bridges.

## Screenshots

### Full dashboard

![SRNE Solar Monitor full dashboard](docs/screenshots/dashboard.png)

### Desktop widget

![SRNE Solar Monitor desktop widget](docs/screenshots/widget.png)

## Requirements

### To build the project

- Windows 10 or Windows 11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- An SRNE-compatible Wi-Fi module and controller
- Internet access when using SRNE cloud mode

### To run a published copy

- Windows 10 or Windows 11
- [.NET 9 Desktop Runtime, Windows x64](https://dotnet.microsoft.com/download/dotnet/9.0)

## Get the source

```powershell
git clone https://github.com/mustafa-noman/SRNE-Solar-Monitor.git
cd SRNE-Solar-Monitor
```

## Configure your device

Find the eight-character Wi-Fi device ID in the SRNE mobile app under:

```text
Device → Basic Info → Device ID
```

Run the full dashboard with your device ID:

```powershell
dotnet run -- --device-id YOUR_DEVICE_ID
```

Example:

```powershell
dotnet run -- --device-id A1B2C3D4
```

The device ID must contain exactly eight hexadecimal characters.

You can also set it permanently for your Windows account:

```powershell
[Environment]::SetEnvironmentVariable(
    "SOLAR_DEVICE_ID",
    "YOUR_DEVICE_ID",
    "User"
)
```

Sign out and back in after setting the environment variable.

## Run the application

Full dashboard:

```powershell
dotnet run
```

Compact always-on-top widget:

```powershell
dotnet run -- --widget
```

The widget can be dragged around the screen. Select the arrow button to open
the full dashboard.

## Publish for another Windows PC

From the project folder, create a framework-dependent Windows deployment:

```powershell
dotnet publish -c Release --no-self-contained `
  -p:UseAppHost=false `
  -o ".\SolarMonitor-Publish"
```

Copy the complete `SolarMonitor-Publish` folder to the destination PC. Do not
copy only `SolarPowerMonitor.dll`.

For example, copy it to:

```text
E:\App\SolarMonitor-Publish
```

Install the .NET 9 Desktop Runtime x64 on the destination PC before launching
the application.

## Create a full-dashboard desktop shortcut

Open PowerShell on the destination PC and run:

```powershell
$folder = "E:\App\SolarMonitor-Publish"
$desktop = [Environment]::GetFolderPath("Desktop")
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut("$desktop\Solar Power Monitor.lnk")

$link.TargetPath = "C:\Program Files\dotnet\dotnet.exe"
$link.Arguments = "`"$folder\SolarPowerMonitor.dll`""
$link.WorkingDirectory = $folder
$link.Save()
```

## Create a widget without a console window

Launching a managed DLL directly through `dotnet.exe` may display a blank
console window. The following hidden launcher prevents that.

Create `StartWidget.vbs`:

```powershell
$folder = "E:\App\SolarMonitor-Publish"
$launcher = "$folder\StartWidget.vbs"

@'
Set shell = CreateObject("WScript.Shell")
shell.Run """C:\Program Files\dotnet\dotnet.exe"" ""E:\App\SolarMonitor-Publish\SolarPowerMonitor.dll"" --widget", 0, False
'@ | Set-Content -LiteralPath $launcher
```

Create desktop and automatic-startup shortcuts:

```powershell
$folder = "E:\App\SolarMonitor-Publish"
$launcher = "$folder\StartWidget.vbs"
$desktop = [Environment]::GetFolderPath("Desktop")
$startup = [Environment]::GetFolderPath("Startup")
$shell = New-Object -ComObject WScript.Shell

foreach ($path in @(
    "$desktop\Solar Power Widget.lnk",
    "$startup\Solar Power Widget.lnk"
)) {
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = "C:\Windows\System32\wscript.exe"
    $link.Arguments = "`"$launcher`""
    $link.WorkingDirectory = $folder
    $link.Save()
}
```

The widget will start automatically the next time the user signs into
Windows.

To disable automatic startup, press `Win + R`, enter `shell:startup`, and
delete the `Solar Power Widget` shortcut.

## Smart App Control

Windows Smart App Control may block an unsigned
`SolarPowerMonitor.exe`. This repository therefore disables the generated
native app host and launches the managed DLL through Microsoft's signed
`dotnet.exe`.

Recommended command:

```powershell
dotnet SolarPowerMonitor.dll
```

Do not disable Windows security features just to run this application. A
publicly distributed native executable should be signed with a trusted
code-signing certificate.

## Direct connection mode

For a transparent Modbus TCP/serial bridge:

```powershell
dotnet run -- `
  --source direct `
  --host 192.168.10.167 `
  --port 8899 `
  --slave 255
```

The bridge must use serial settings compatible with the controller. The
application sends complete Modbus RTU request frames and expects complete RTU
response frames.

## Command-line options

| Option | Purpose | Default |
| --- | --- | --- |
| `--widget` | Open the compact desktop widget | Disabled |
| `--source` | Select `cloud` or `direct` mode | `cloud` |
| `--device-id` | Eight-character SRNE Wi-Fi device ID | Project default |
| `--host` | Direct bridge IP address or hostname | `192.168.10.167` |
| `--port` | Direct bridge TCP port | `8899` |
| `--slave` | Modbus slave address | `255` |
| `--poll-ms` | Direct-mode polling interval | `2000` |
| `--connect-timeout-ms` | Connection timeout | `5000` |
| `--response-timeout-ms` | Telemetry timeout | `15000` |
| `--reconnect-delay-ms` | Delay before reconnecting | `2000` |
| `--self-test` | Run protocol tests and exit | Disabled |

Equivalent environment variables use the `SOLAR_` prefix, including:

- `SOLAR_DEVICE_ID`
- `SOLAR_SOURCE`
- `SOLAR_HOST`
- `SOLAR_PORT`
- `SOLAR_SLAVE_ID`

## Protocol verification

Run the offline protocol tests:

```powershell
dotnet run -- --self-test
```

The tests verify:

- Modbus request bytes and CRC-16
- Register parsing and scale factors
- Rejection of corrupted responses

No physical controller is required for these tests.

## Security and privacy

- No SRNE MQTT username or password is stored in this repository.
- MQTT connection details are requested from the SRNE service at runtime.
- Do not commit account passwords, tokens, private certificates, or diagnostic
  output containing credentials.
- A device ID identifies hardware and should be replaced with your own when
  deploying the application.

## AI use

This project was developed with assistance from
[OpenAI Codex](https://openai.com/codex/). Codex was used for implementation,
debugging, UI development, protocol testing, and documentation. Project
direction, device configuration, testing decisions, and repository ownership
remain with Mustafa Noman.

## Troubleshooting

### The shortcut does nothing

Confirm that this file exists:

```text
C:\Program Files\dotnet\dotnet.exe
```

If it does not, install the .NET 9 Desktop Runtime x64.

### The application stays on CONNECTING

- Confirm that the computer has internet access.
- Confirm that the device is online in the SRNE mobile app.
- Check that the configured device ID is correct.
- Allow outbound HTTP and MQTT connections through the firewall.

### Smart App Control blocks the executable

Use the DLL shortcut described above instead of launching an unsigned `.exe`.

### The widget opens with a blank console

Use the `StartWidget.vbs` hidden launcher and `wscript.exe` shortcut described
above.

## Contributing

Issues and pull requests are welcome. Before submitting a change:

```powershell
dotnet build -c Release
dotnet run -c Release --no-build -- --self-test
```

Please avoid including real credentials or private device information in bug
reports.

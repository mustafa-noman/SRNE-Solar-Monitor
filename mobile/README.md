# Solar Monitor for Android

.NET 10 MAUI dashboard for Android 13 and newer.

## Features

- Solar generation, PV voltage/current, battery state, voltage, and charge current
- Live session graph and latest 50 readings
- SRNE cloud telemetry as the production default
- Device identity encrypted with Android Keystore
- MQTT credentials retained only in process memory
- Demo mode available for offline preview
- Direct Modbus RTU-over-TCP connection to a controller bridge
- No embedded device ID, MQTT username, password, or cloud credential lookup

## Android support

- Minimum: Android 13 / API 33
- Target: API 36 with the current .NET Android workload

## Build

Install .NET 10, the MAUI Android workload, and Microsoft OpenJDK 21. The script installs Android SDK packages into ignored workspace folder `.tools` when needed:

```powershell
.\mobile\initialize-signing.ps1
.\mobile\build-android.ps1
```

Run signing initialization once. It creates release signing material under `%USERPROFILE%\.android\solar-monitor`; securely back up both files because all future app updates require the same key.

Release APK output:

```text
mobile\SolarPowerMonitor.Mobile\bin\Release\net10.0-android\publish\com.mustafanoman.solarmonitor-Signed.apk
```

On first launch, open **Settings** and enter the eight-character SRNE Wi-Fi device ID. Use **Direct LAN** instead when a Modbus TCP bridge is reachable from the phone.

The SRNE vendor configuration endpoint uses HTTP. Android cleartext traffic remains disabled globally and is enabled only for `www.srne.net`; this vendor limitation should be considered when deploying outside a trusted network.

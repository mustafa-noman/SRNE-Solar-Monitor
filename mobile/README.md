# Solar Monitor for Android

.NET 10 MAUI dashboard for Android 13 and newer.

## Features

- Solar generation, PV voltage/current, battery state, voltage, and charge current
- Live session graph and latest 50 readings
- Demo mode enabled on first launch
- Direct Modbus RTU-over-TCP connection to a controller bridge
- No embedded device ID, MQTT username, password, or cloud credential lookup

## Android support

- Minimum: Android 13 / API 33
- Target: API 36 with the current .NET Android workload

## Build

Install .NET 10, the MAUI Android workload, and Microsoft OpenJDK 21. The script installs Android SDK packages into ignored workspace folder `.tools` when needed:

```powershell
.\mobile\build-android.ps1
```

Release APK output:

```text
mobile\SolarPowerMonitor.Mobile\bin\Release\net10.0-android\publish\com.mustafanoman.solarmonitor-Signed.apk
```

On first launch, open **Settings** to disable demo mode and enter the local bridge host, port, and Modbus slave ID.

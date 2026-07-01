[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $PSScriptRoot 'SolarPowerMonitor.Mobile\SolarPowerMonitor.Mobile.csproj'
$androidSdk = Join-Path $repositoryRoot '.tools\android-sdk'
$jdk = Get-ChildItem 'C:\Program Files\Microsoft\jdk-*' -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $jdk) {
    throw 'Microsoft OpenJDK is missing. Install it with: winget install Microsoft.OpenJDK.21'
}

if (-not (Test-Path (Join-Path $androidSdk 'platforms'))) {
    Write-Host "Installing Android SDK into $androidSdk ..."
    $installArguments = @(
        'build', $project,
        '-t:InstallAndroidDependencies',
        '-f', 'net10.0-android',
        "-p:AndroidSdkDirectory=$androidSdk",
        "-p:JavaSdkDirectory=$jdk",
        '-p:AcceptAndroidSDKLicenses=True'
    )
    & dotnet @installArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Android SDK installation failed with exit code $LASTEXITCODE."
    }
}

if ($Configuration -eq 'Release') {
    $arguments = @(
        'publish', $project, '-f', 'net10.0-android', '-c', 'Release',
        "-p:AndroidSdkDirectory=$androidSdk",
        "-p:JavaSdkDirectory=$jdk",
        '-p:AndroidPackageFormats=apk'
    )
}
else {
    $arguments = @(
        'build', $project, '-c', 'Debug',
        "-p:AndroidSdkDirectory=$androidSdk",
        "-p:JavaSdkDirectory=$jdk"
    )
}

& dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    throw "Android build failed with exit code $LASTEXITCODE."
}

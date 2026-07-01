[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$jdk = Get-ChildItem 'C:\Program Files\Microsoft\jdk-*' -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $jdk) {
    throw 'Microsoft OpenJDK is missing. Install it with: winget install Microsoft.OpenJDK.21'
}

$signingDirectory = Join-Path $env:USERPROFILE '.android\solar-monitor'
$keyStore = Join-Path $signingDirectory 'solar-monitor-release.keystore'
$passwordFile = Join-Path $signingDirectory 'signing-password.txt'

if ((Test-Path $keyStore) -or (Test-Path $passwordFile)) {
    throw "Signing material already exists at $signingDirectory. Existing keys will not be overwritten."
}

New-Item -ItemType Directory -Force -Path $signingDirectory | Out-Null
$randomBytes = [byte[]]::new(32)
[Security.Cryptography.RandomNumberGenerator]::Fill($randomBytes)
$password = [Convert]::ToHexString($randomBytes)
Set-Content -LiteralPath $passwordFile -Value $password -NoNewline

$keyArguments = @(
    '-genkeypair', '-v',
    '-keystore', $keyStore,
    '-alias', 'solar-monitor-release',
    '-keyalg', 'RSA',
    '-keysize', '4096',
    '-validity', '10000',
    '-dname', 'CN=Solar Monitor, O=Mustafa Noman, C=BD',
    '-storepass', $password,
    '-keypass', $password
)
& (Join-Path $jdk 'bin\keytool.exe') @keyArguments

if ($LASTEXITCODE -ne 0) {
    throw "keytool failed with exit code $LASTEXITCODE."
}

Write-Host "Production signing material created in $signingDirectory"
Write-Warning 'Back up both files securely. Losing this key prevents future updates to installed production builds.'

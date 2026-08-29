# start-uat.ps1 — Start the Angor Android app for UAT testing
# Run this before: dotnet test ... -e ANGOR_UAT_HOST=android
#
# Prerequisites:
#   - Debug APK installed on device
#   - Device shows as "device" in adb devices (not "unauthorized")

param(
    [string]$Adb = (Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"),
    [int]$Port = 18721
)

Write-Host "=== Angor Android UAT Setup ===" -ForegroundColor Cyan
Write-Host "adb: $Adb"

function Adb-Run([string]$args_) {
    $output = & $Adb $args_.Split(' ') 2>&1 | Out-String
    return $output.Trim()
}

function Wait-Device {
    Write-Host "Waiting for device..." -NoNewline
    & $Adb wait-for-device 2>&1 | Out-Null
    Start-Sleep -Seconds 1
    Write-Host " OK" -ForegroundColor Green
}

# Verify device
Wait-Device
$devices = Adb-Run "devices"
if ($devices -notmatch "\sdevice\b") {
    Write-Host "ERROR: No authorized device found." -ForegroundColor Red
    Write-Host $devices
    exit 1
}
Write-Host "Device found." -ForegroundColor Green

# Enable automation server (set prop BEFORE launching so it's read at startup)
Adb-Run "shell setprop debug.angor.test_api 1"

# Stop any running instance (may briefly drop USB on some devices)
& $Adb shell am force-stop io.angor.app 2>&1 | Out-Null
Start-Sleep -Seconds 2
Wait-Device

# Launch
& $Adb shell monkey -p io.angor.app 1 2>&1 | Out-Null
Start-Sleep -Seconds 2

# Forward port
& $Adb forward "tcp:$Port" "tcp:$Port" 2>&1 | Out-Null

# Wait for health
Write-Host "Waiting for automation server..." -NoNewline
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    try {
        $r = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 3
        if ($r -match "ready") { $ready = $true; break }
    } catch {}
    Write-Host "." -NoNewline
}
Write-Host ""

if ($ready) {
    Write-Host "Ready on http://127.0.0.1:$Port" -ForegroundColor Green
    Write-Host ""
    Write-Host "Run tests with:" -ForegroundColor Yellow
    Write-Host '  $env:ANGOR_UAT_HOST = "android"'
    Write-Host '  dotnet test src/design/App.Test.Uat/App.Test.Uat.csproj --filter "FullyQualifiedName~CreateProjectTest" -c Debug'
} else {
    Write-Host "ERROR: Automation server not responding." -ForegroundColor Red
    Write-Host "Check that the Debug APK is installed and debug.angor.test_api is set."
    exit 1
}

param (
    [string]$BaseUrl = "http://localhost:5000",
    [string]$K6Path = "$env:TEMP\k6.exe"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FleetOps API — Empirical Performance Benchmark Runner" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target Base URL: $BaseUrl"
Write-Host "K6 Executable:   $K6Path"

if (-not (Test-Path $K6Path)) {
    $foundK6 = Get-Command k6 -ErrorAction SilentlyContinue
    if ($foundK6) {
        $K6Path = $foundK6.Source
    } else {
        Write-Error "k6 binary not found at '$K6Path' or in PATH. Please run installation."
        exit 1
    }
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "`n[1/3] Executing Baseline Load Test (5 VUs, 30s)..." -ForegroundColor Yellow
& $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-baseline.js"

Write-Host "`n[2/3] Executing Sustained Load Test (15 VUs, 30s)..." -ForegroundColor Yellow
& $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-sustained.js"

Write-Host "`n[3/3] Executing Spike Load Test (Ramp to 40 VUs)..." -ForegroundColor Yellow
& $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-spike.js"

Write-Host "`nAll benchmark scenarios completed successfully." -ForegroundColor Green

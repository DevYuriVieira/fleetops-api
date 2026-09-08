param (
    [string]$BaseUrl = "http://localhost:5000",
    [string]$K6Path = "$env:TEMP\k6.exe",
    [ValidateSet("Health", "Domain", "Stress", "RateLimit", "All")]
    [string]$Suite = "All"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FleetOps API — Empirical Performance Benchmark Runner" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target Base URL: $BaseUrl"
Write-Host "K6 Executable:   $K6Path"
Write-Host "Selected Suite:  $Suite"

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

# 1. Health / In-Memory Baseline Suite
if ($Suite -eq "Health" -or $Suite -eq "All") {
    Write-Host "`n--- [SUITE: HEALTH / IN-MEMORY BASELINE] ---" -ForegroundColor Magenta
    Write-Host "`n[1/3] Executing Health Baseline Load Test (5 VUs, 30s)..." -ForegroundColor Yellow
    & $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-baseline.js"

    Write-Host "`n[2/3] Executing Health Sustained Load Test (15 VUs, 30s)..." -ForegroundColor Yellow
    & $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-sustained.js"

    Write-Host "`n[3/3] Executing Health Spike Load Test (Ramp to 40 VUs)..." -ForegroundColor Yellow
    & $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-spike.js"
}

# 2. Rate Limiting Suite
if ($Suite -eq "RateLimit" -or $Suite -eq "All") {
    Write-Host "`n--- [SUITE: IN-PROCESS RATE LIMITING BURST] ---" -ForegroundColor Magenta
    Write-Host "`nExecuting Rate Limiting Burst Test (10 VUs, 10s)..." -ForegroundColor Yellow
    & $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-ratelimit-burst.js"
}

# 3. Domain Capacity Suite (Authenticated Database Writes + Outbox)
if ($Suite -eq "Domain" -or $Suite -eq "All") {
    Write-Host "`n--- [SUITE: DOMAIN CAPACITY BENCHMARKS] ---" -ForegroundColor Magenta
    Write-Host "`n[1/3] Executing Domain Baseline Load Test (5 VUs, 30s)..." -ForegroundColor Yellow
    & $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-domain-baseline.js"

    Write-Host "`n[2/3] Executing Domain Sustained Load Test (15 VUs, 45s)..." -ForegroundColor Yellow
    & $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-domain-sustained.js"

    Write-Host "`n[3/3] Executing Domain Spike Load Test (5 -> 35 -> 5 VUs)..." -ForegroundColor Yellow
    & $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-domain-spike.js"
}

# 4. Stress / Saturation Suite
if ($Suite -eq "Stress" -or $Suite -eq "All") {
    Write-Host "`n--- [SUITE: DOMAIN STRESS & SATURATION BENCHMARK] ---" -ForegroundColor Magenta
    Write-Host "`nExecuting Progressive Stress Test (5 -> 15 -> 30 -> 50 -> 70 VUs)..." -ForegroundColor Yellow
    & $K6Path run --env API_BASE_URL=$BaseUrl "$ScriptDir\k6-domain-stress.js"
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  All selected benchmark scenarios completed successfully." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

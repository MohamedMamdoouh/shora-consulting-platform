# Post-deploy verification (spec 09 + production-config § Verify)
#
# Usage:
#   .\scripts\post-deploy-verify.ps1 -BaseUrl https://app-shora-prod.azurewebsites.net

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $BaseUrl
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$failures = @()

function Test-Endpoint {
    param(
        [string] $Name,
        [string] $Url,
        [scriptblock] $Validate
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 60
        & $Validate $response
        Write-Host "[OK] $Name — $Url" -ForegroundColor Green
    }
    catch {
        Write-Host "[FAIL] $Name — $Url" -ForegroundColor Red
        Write-Host "       $($_.Exception.Message)" -ForegroundColor Red
        $script:failures += $Name
    }
}

Write-Host "Verifying Shora deployment at $BaseUrl ..." -ForegroundColor Cyan

Test-Endpoint -Name 'API health' -Url "$BaseUrl/api/v1/health" -Validate {
    param($r)
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode)" }
    $body = $r.Content | ConvertFrom-Json
    if ($body.status -ne 'healthy') { throw "Expected healthy status" }
}

Test-Endpoint -Name 'SPA home' -Url "$BaseUrl/" -Validate {
    param($r)
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode)" }
    if ($r.Content -notmatch 'app-root|<!doctype html>') { throw 'Expected Angular shell HTML' }
}

Test-Endpoint -Name 'SPA client route fallback' -Url "$BaseUrl/about" -Validate {
    param($r)
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode)" }
    if ($r.Content -notmatch 'app-root|<!doctype html>') { throw 'Expected SPA fallback HTML' }
}

Test-Endpoint -Name 'Public settings API' -Url "$BaseUrl/api/v1/settings/public" -Validate {
    param($r)
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode)" }
    $body = $r.Content | ConvertFrom-Json
    if ($null -eq $body.sessionPrice) { throw 'Missing sessionPrice' }
}

Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'All smoke checks passed.' -ForegroundColor Green
    Write-Host 'Manual steps remaining:'
    Write-Host '  - Admin login and verify /admin/settings payment numbers'
    Write-Host '  - E2E: signup → verify email → book → receipt → admin approve'
    Write-Host '  - Remove AdminSeed__* from App Service after admin works'
    exit 0
}

Write-Host "Failed checks: $($failures -join ', ')" -ForegroundColor Red
exit 1

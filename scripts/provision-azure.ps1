# Provision Shora production Azure resources (spec 09.7)
# Requires: Azure CLI (`az`) logged in with an active subscription.
#
# Usage:
#   .\scripts\provision-azure.ps1 -BaseName shora -Location westeurope -SqlAdminPassword 'YourStrongP@ssw0rd!'
#
# After deploy, run:
#   .\scripts\set-app-settings.ps1 -ResourceGroup rg-shora-prod -WebAppName app-shora-prod -SqlPassword '...'

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9]{3,16}$')]
    [string] $BaseName,

    [string] $Location = 'westeurope',

    [Parameter(Mandatory = $true)]
    [string] $SqlAdminPassword,

    [string] $CustomDomain = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$infraDir = Join-Path $repoRoot 'infra'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error 'Azure CLI (az) is required. Install from https://learn.microsoft.com/cli/azure/install-azure-cli'
}

$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Error 'Not logged in to Azure. Run: az login'
}

Write-Host "Deploying Shora infra to subscription $($account.name) ($($account.id))..." -ForegroundColor Cyan

$deployArgs = @(
    'deployment', 'sub', 'create',
    '--location', $Location,
    '--template-file', (Join-Path $infraDir 'main.bicep'),
    '--parameters', (Join-Path $infraDir 'main.bicepparam'),
    '--parameters', "baseName=$BaseName",
    '--parameters', "location=$Location",
    '--parameters', "sqlAdminPassword=$SqlAdminPassword",
    '--parameters', "customDomain=$CustomDomain",
    '--output', 'json'
)

$result = az @deployArgs | ConvertFrom-Json
$outputs = $result.properties.outputs

Write-Host ''
Write-Host '=== Deployment complete ===' -ForegroundColor Green
Write-Host "Resource group: $($outputs.resourceGroupName.value)"
Write-Host "Web App name:   $($outputs.webAppName.value)  (set GitHub variable AZURE_WEBAPP_NAME)"
Write-Host "Default URL:    https://$($outputs.webAppDefaultHostName.value)"
Write-Host "SQL server:     $($outputs.sqlServerFqdn.value)"
Write-Host "Storage:        $($outputs.storageAccountName.value) / $($outputs.receiptsContainerName.value)"
Write-Host ''
Write-Host 'Next steps:'
Write-Host '  1. Get publish profile from Portal → App Service → Get publish profile → GitHub secret AZURE_WEBAPP_PUBLISH_PROFILE'
Write-Host '  2. Run scripts\set-app-settings.ps1 with JWT, SMTP, and admin seed values'
Write-Host '  3. Merge to main to trigger Deploy workflow'

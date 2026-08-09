# Set Shora App Service application settings (spec 09.6)
# Requires: Azure CLI (`az`)
#
# Usage:
#   .\scripts\set-app-settings.ps1 `
#     -ResourceGroup rg-shora-prod `
#     -WebAppName app-shora-prod `
#     -SqlServerFqdn sql-shora-prod.database.windows.net `
#     -SqlDatabase shora `
#     -SqlAdminLogin shoraadmin `
#     -SqlAdminPassword '...' `
#     -StorageAccountName stshoraprod `
#     -JwtSigningKey '...' `
#     -FrontendBaseUrl 'https://app-shora-prod.azurewebsites.net' `
#     -SmtpHost smtp.sendgrid.net `
#     -SmtpFromAddress noreply@yourdomain.com `
#     -SmtpUsername apikey `
#     -SmtpPassword 'SG.xxx' `
#     -AdminSeedEmail admin@yourdomain.com `
#     -AdminSeedPassword '...' `
#     -GoogleClientId 'xxx.apps.googleusercontent.com'

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string] $WebAppName,

    [Parameter(Mandatory = $true)]
    [string] $SqlServerFqdn,

    [string] $SqlDatabase = 'shora',

    [Parameter(Mandatory = $true)]
    [string] $SqlAdminLogin,

    [Parameter(Mandatory = $true)]
    [string] $SqlAdminPassword,

    [Parameter(Mandatory = $true)]
    [string] $StorageAccountName,

    [string] $ReceiptContainer = 'receipts',

    [Parameter(Mandatory = $true)]
    [string] $JwtSigningKey,

    [Parameter(Mandatory = $true)]
    [string] $FrontendBaseUrl,

    [Parameter(Mandatory = $true)]
    [string] $SmtpHost,

    [Parameter(Mandatory = $true)]
    [string] $SmtpFromAddress,

    [string] $SmtpPort = '587',

    [string] $SmtpUsername = '',

    [string] $SmtpPassword = '',

    [string] $AdminSeedEmail = '',

    [string] $AdminSeedPassword = '',

    [string] $GoogleClientId = '',

    [string] $ConsultantWhatsApp = '',

    [string] $VodafoneCashNumber = '',

    [string] $InstaPayHandle = ''
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error 'Azure CLI (az) is required.'
}

$storageKey = az storage account keys list `
    --resource-group $ResourceGroup `
    --account-name $StorageAccountName `
    --query '[0].value' -o tsv

$storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=$StorageAccountName;AccountKey=$storageKey;EndpointSuffix=core.windows.net"
$sqlConnectionString = "Server=tcp:$SqlServerFqdn,1433;Initial Catalog=$SqlDatabase;Persist Security Info=False;User ID=$SqlAdminLogin;Password=$SqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

$settings = [ordered]@{
    'ASPNETCORE_ENVIRONMENT' = 'Production'
    'ConnectionStrings__DefaultConnection' = $sqlConnectionString
    'Jwt__SigningKey' = $JwtSigningKey
    'Storage__ConnectionString' = $storageConnectionString
    'Storage__ReceiptContainer' = $ReceiptContainer
    'Frontend__BaseUrl' = $FrontendBaseUrl.TrimEnd('/')
    'Cors__AllowedOrigins__0' = $FrontendBaseUrl.TrimEnd('/')
    'Email__Host' = $SmtpHost
    'Email__Port' = $SmtpPort
    'Email__FromAddress' = $SmtpFromAddress
}

if ($SmtpUsername) { $settings['Email__Username'] = $SmtpUsername }
if ($SmtpPassword) { $settings['Email__Password'] = $SmtpPassword }
if ($AdminSeedEmail) { $settings['AdminSeed__Email'] = $AdminSeedEmail }
if ($AdminSeedPassword) { $settings['AdminSeed__Password'] = $AdminSeedPassword }
if ($GoogleClientId) { $settings['Google__ClientId'] = $GoogleClientId }
if ($ConsultantWhatsApp) { $settings['Seed__ConsultantWhatsAppNumber'] = $ConsultantWhatsApp }
if ($VodafoneCashNumber) { $settings['Seed__VodafoneCashNumber'] = $VodafoneCashNumber }
if ($InstaPayHandle) { $settings['Seed__InstaPayHandle'] = $InstaPayHandle }

$settingArgs = @()
foreach ($key in $settings.Keys) {
    $settingArgs += "$key=$($settings[$key])"
}

Write-Host "Applying $($settings.Count) app settings to $WebAppName..." -ForegroundColor Cyan
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --settings @settingArgs `
    --output none

Write-Host 'App settings applied. Restart the web app if it is already running.' -ForegroundColor Green

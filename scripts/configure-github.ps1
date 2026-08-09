# Configure GitHub repository for Shora Deploy workflow (spec 09.9)
# Requires: GitHub CLI (`gh`) authenticated with repo admin access.
#
# Usage:
#   .\scripts\configure-github.ps1 -WebAppName app-shora-prod -PublishProfilePath .\publishProfile.xml
#   .\scripts\configure-github.ps1 -WebAppName app-shora-prod -GoogleClientId 'xxx.apps.googleusercontent.com'

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $WebAppName,

    [string] $PublishProfilePath = '',

    [string] $GoogleClientId = '',

    [string] $DeployEnvironment = 'production',

    [switch] $EnableBranchProtection
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error 'GitHub CLI (gh) is required. Install from https://cli.github.com/'
}

gh auth status 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error 'Not authenticated. Run: gh auth login'
}

Write-Host "Setting repository variable AZURE_WEBAPP_NAME=$WebAppName" -ForegroundColor Cyan
gh variable set AZURE_WEBAPP_NAME --body $WebAppName

Write-Host "Setting repository variable DEPLOY_ENVIRONMENT=$DeployEnvironment" -ForegroundColor Cyan
gh variable set DEPLOY_ENVIRONMENT --body $DeployEnvironment

if ($GoogleClientId) {
    Write-Host "Setting repository variable GOOGLE_CLIENT_ID" -ForegroundColor Cyan
    gh variable set GOOGLE_CLIENT_ID --body $GoogleClientId
}

Write-Host "Ensuring GitHub environment '$DeployEnvironment' exists..." -ForegroundColor Cyan
gh api "repos/{owner}/{repo}/environments/$DeployEnvironment" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    gh api --method PUT "repos/{owner}/{repo}/environments/$DeployEnvironment" | Out-Null
}

if ($PublishProfilePath) {
    if (-not (Test-Path $PublishProfilePath)) {
        Write-Error "Publish profile not found: $PublishProfilePath"
    }
    $profile = Get-Content -Raw -Path $PublishProfilePath
    Write-Host "Setting environment secret AZURE_WEBAPP_PUBLISH_PROFILE on '$DeployEnvironment'" -ForegroundColor Cyan
    gh secret set AZURE_WEBAPP_PUBLISH_PROFILE --env $DeployEnvironment --body $profile
}
else {
    Write-Warning 'PublishProfilePath not provided — set AZURE_WEBAPP_PUBLISH_PROFILE manually in GitHub → Environments → production'
}

if ($EnableBranchProtection) {
    Write-Host 'Enabling branch protection on main (requires admin)...' -ForegroundColor Cyan
    $repo = gh repo view --json nameWithOwner -q .nameWithOwner
    $protection = @{
        required_status_checks = @{
            strict = $true
            checks = @(
                @{ context = 'Backend' },
                @{ context = 'Frontend' }
            )
        }
        enforce_admins = $false
    } | ConvertTo-Json -Depth 5 -Compress
    $protection | gh api --method PUT "repos/$repo/branches/main/protection" --input -
}

Write-Host ''
Write-Host '=== GitHub configuration complete ===' -ForegroundColor Green
Write-Host 'Verify: Settings → Secrets and variables → Actions'

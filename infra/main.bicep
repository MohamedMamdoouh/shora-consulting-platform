// Shora production infrastructure (spec 09.7)
// Deploy: scripts/provision-azure.ps1

targetScope = 'subscription'

@description('Azure region, e.g. westeurope or uaenorth')
param location string = 'westeurope'

@description('Base name for resources (lowercase alphanumeric, 3–16 chars)')
@minLength(3)
@maxLength(16)
param baseName string

@description('SQL admin login (not used when Entra-only; kept for ADO.NET SQL auth MVP)')
param sqlAdminLogin string = 'shoraadmin'

@secure()
@description('SQL admin password — min 12 chars, mixed case, numbers, symbols')
param sqlAdminPassword string

@description('Optional custom domain for CORS / Frontend__BaseUrl hint output')
param customDomain string = ''

var resourceTags = {
  app: 'shora'
  environment: 'production'
}

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${baseName}-prod'
  location: location
  tags: resourceTags
}

module core 'modules/core.bicep' = {
  name: 'shora-core'
  scope: rg
  params: {
    location: location
    baseName: baseName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    tags: resourceTags
  }
}

output resourceGroupName string = rg.name
output webAppName string = core.outputs.webAppName
output webAppDefaultHostName string = core.outputs.webAppDefaultHostName
output sqlServerFqdn string = core.outputs.sqlServerFqdn
output sqlDatabaseName string = core.outputs.sqlDatabaseName
output storageAccountName string = core.outputs.storageAccountName
output receiptsContainerName string = core.outputs.receiptsContainerName
output suggestedFrontendBaseUrl string = empty(customDomain) ? 'https://${core.outputs.webAppDefaultHostName}' : 'https://${customDomain}'
output connectionStringSettingHint string = 'Set ConnectionStrings__DefaultConnection in App Service (ADO.NET from Portal or scripts/set-app-settings.ps1)'

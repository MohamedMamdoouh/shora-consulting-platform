# Shora production infrastructure

Bicep templates for Azure App Service, SQL Database, and Blob Storage (spec 09.7).

## Deploy

```powershell
az login
.\scripts\provision-azure.ps1 -BaseName shora -Location westeurope -SqlAdminPassword 'YourStrongP@ssw0rd!'
```

## Resources created

| Resource | Name pattern | Notes |
| --- | --- | --- |
| Resource group | `rg-{baseName}-prod` | |
| App Service Plan | `asp-{baseName}-prod` | Linux B1, 1 instance |
| Web App | `app-{baseName}-prod` | .NET 10, Always On, HTTPS only |
| SQL server + DB | `sql-{baseName}-prod` / `shora` | Basic tier, 7-day backup retention |
| Storage account | `st{baseName}prod` | Private `receipts` container |

Set `AZURE_WEBAPP_NAME` to the Web App name output after deploy.

# Azure prerequisites (spec 09.7)

Create production resources in **Azure Portal** and configure GitHub **before** merging to `main` (which triggers Deploy automatically).

**Related:** [production-config.md](production-config.md) (app settings) · [docs/README.md](README.md) (full go-live order)

## Azure MCP plugin (optional)

In **Cursor**, authenticate the **Azure MCP** plugin and ask the agent to provision resources or configure App Service settings using the checklists below. Requires an active Azure subscription signed in through MCP.

Example: *"Using Azure MCP, create Shora production resources per azure-prerequisites.md in region westeurope."*

Manual **Azure Portal** steps work the same way — follow the sections below.

## Go-live checklist

### Azure Portal

- [ ] App Service Plan (Linux, 1 instance) + Web App (.NET 10, Always On, HTTPS only)
- [ ] Azure SQL server + database; firewall allows Azure services
- [ ] Storage account + **private** blob container (e.g. `receipts`)
- [ ] App Service application settings — full list in [production-config.md](production-config.md), including:
  - [ ] `ASPNETCORE_ENVIRONMENT`, SQL connection, `Jwt__SigningKey` (32+ chars)
  - [ ] `Storage__ConnectionString`, `Storage__ReceiptContainer`
  - [ ] `Frontend__BaseUrl` and `Cors__AllowedOrigins__0` (same HTTPS URL)
  - [ ] `Email__*` SMTP (required for email verification / booking)
  - [ ] `AdminSeed__*` (one-time admin bootstrap — remove after first login)
  - [ ] `Seed__*` payment defaults (optional — or update `/admin/settings` after deploy)
  - [ ] `Google__ClientId` (optional — if using Google sign-in)

### Frontend and Google (if using Google sign-in)

- [ ] `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts)
- [ ] Google Cloud Console: Authorized JavaScript origins = production HTTPS URL — [production-config.md § Google Cloud setup](production-config.md#google-cloud-setup)

### GitHub

- [ ] Repository variable: `AZURE_WEBAPP_NAME` (exact Web App name from Portal)
- [ ] `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) (build-time; commit before merge)
- [ ] GitHub Environment: `production` (optional required reviewers)
- [ ] Environment secret: `AZURE_WEBAPP_PUBLISH_PROFILE` (App Service → **Get publish profile**)
- [ ] Optional repository variable: `DEPLOY_ENVIRONMENT` (defaults to `production` if unset)
- [ ] Branch protection on `main` — require CI `Backend` and `Frontend` checks before merge

### First deploy and verification

- [ ] Merge to `main` (triggers **Deploy** automatically — no manual workflow run)
- [ ] Confirm `https://<host>/` and `https://<host>/api/v1/health`
- [ ] Log in as admin; update `/admin/settings` (payment numbers, WhatsApp) if not preset via `Seed__*`
- [ ] Remove `AdminSeed__*` from App Service after admin login works

---

## Resources to create

| Resource            | SKU / settings                                        | Purpose                                                 |
| ------------------- | ----------------------------------------------------- | ------------------------------------------------------- |
| **App Service**     | Linux, .NET 10, **Always On**, **instance count = 1** | API + Angular static files + in-process background jobs |
| **Azure SQL**       | Server + database (Basic OK for MVP)                  | `ConnectionStrings__DefaultConnection`                  |
| **Storage account** | Standard LRS, HTTPS only, no public blob access       | Receipt blobs (`Storage__ReceiptContainer`, spec 05)    |

**Note:** Confirm **.NET 10** (`DOTNETCORE|10.0`) is available in your App Service region before provisioning.

## App Service

1. Create a **Linux** App Service Plan (B1 is fine for MVP; **scale out = 1**).
2. Create the Web App on that plan.
3. **Configuration → General settings:**

- Stack: **.NET 10** (`DOTNETCORE|10.0` on Linux)
- **Always On:** On (background jobs, spec 08 §3)
- **HTTPS Only:** On

4. **Configuration → Application settings** — see [production-config.md](production-config.md). Minimum to start the app:

| Setting                                | Example / source                         |
| -------------------------------------- | ---------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`               | `Production`                             |
| `ConnectionStrings__DefaultConnection` | Azure SQL → Connection strings → ADO.NET |
| `Jwt__SigningKey`                      | Generate locally; min 32 characters      |

Minimum for a working MVP (add before go-live):

| Setting                   | Example / source                       |
| ------------------------- | -------------------------------------- |
| `Storage__ConnectionString` | Storage account → Access keys        |
| `Storage__ReceiptContainer` | `receipts`                           |
| `Frontend__BaseUrl`       | `https://<app-name>.azurewebsites.net` |
| `Cors__AllowedOrigins__0` | Same as `Frontend__BaseUrl`            |
| `Email__Host`, `Email__FromAddress`, … | SMTP provider (spec 08.4)   |
| `AdminSeed__Email`, `AdminSeed__Password` | One-time admin (remove after login) |
| `Google__ClientId`        | Google Cloud console (optional)        |
| `Seed__VodafoneCashNumber`, etc. | Real payment numbers (optional pre-seed) |

5. **Scale out (App Service plan):** keep at **1 instance** — no autoscaling for MVP.

## Azure SQL

1. Create SQL server + database.
2. **Networking → Firewall:** enable **Allow Azure services and resources to access this server**.
3. Copy ADO.NET connection string into App Service `ConnectionStrings__DefaultConnection`.
4. **Backup:** Basic tier includes 7-day automated backups. Confirm under **Database → Backups**. For production, document restore procedure before first schema migration.

## Blob storage

1. Create storage account.
2. Disable anonymous public blob access.
3. Create container `receipts` (or your chosen name) with **private** access.
4. Set `Storage__ConnectionString` and matching `Storage__ReceiptContainer` on App Service.

## GitHub (spec 09.9)

| Item              | Where                                  | Value                                          |
| ----------------- | -------------------------------------- | ---------------------------------------------- |
| Web App name      | Repository **Variables**               | Exact name from Azure Portal                   |
| Publish profile   | Environment **Secrets** (`production`) | App Service → **Get publish profile**          |
| Deploy environment | Repository **Variables** (optional)   | `DEPLOY_ENVIRONMENT` — defaults to `production`  |
| Approval gate     | **Environments → production**          | Optional required reviewers                    |

Deploy triggers on **push to `main` only** — there is no manual workflow dispatch. Configure Azure and GitHub secrets **before** the first merge to `main`.

Optional later: Azure OIDC federated credential instead of publish profile.

## Branch protection

After CI has passed at least once on `main`:

1. **Settings → Branches → Add rule** for `main`
2. **Require status checks:** `Backend` and `Frontend`
3. **Require branches to be up to date before merging** (recommended)

See [`.github/workflows/README.md`](../.github/workflows/README.md).

## Verify

1. App settings saved; no secrets in git.
2. Push (or merge) to `main` triggers **Deploy** automatically.
3. Open `https://<webAppName>.azurewebsites.net/` (SPA) and `/api/v1/health` (API).
4. Complete post-deploy steps in [production-config.md § Verify](production-config.md#verify).

First deploy runs EF migrations and idempotent seed on app startup (spec 09.10) — no separate migration step in the pipeline.

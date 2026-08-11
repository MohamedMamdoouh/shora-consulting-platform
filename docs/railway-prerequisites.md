# Railway production prerequisites (Path B-lite)

Deploy Shora to **Railway** (API + Angular SPA in one container) with **Neon PostgreSQL** (database) and **Azure Blob Storage** (receipts only).

**Related:** [production-config.md](production-config.md) · [`.github/workflows/README.md`](../.github/workflows/README.md)

## Architecture

| Component | Provider |
| --- | --- |
| Compute (API + SPA) | Railway |
| Database | Neon PostgreSQL (free tier) |
| Receipt blobs | Azure Storage (`stshoraprodne001` / `receipts`) |
| Email | SendGrid SMTP |

Production URL is your Railway domain, e.g. `https://shora-production.up.railway.app`.

---

## B1 — Neon + Azure storage

### Neon PostgreSQL

1. [neon.tech](https://neon.tech) → create a project (e.g. `shora-prod`).
2. Create a database (default `neondb` or rename to `Shora`).
3. Copy the **pooled** connection string from the Neon dashboard (recommended for Railway serverless-style restarts).
4. Ensure SSL is enabled — append `Ssl Mode=Require` if not already in the string.
5. Set on Railway: `ConnectionStrings__DefaultConnection` = Neon connection string.

Example formats:

```text
Host=ep-xxx.region.aws.neon.tech;Database=neondb;Username=...;Password=...;Ssl Mode=Require
```

Or URI form (Npgsql accepts both):

```text
postgresql://user:password@ep-xxx.region.aws.neon.tech/neondb?sslmode=require
```

On first deploy, EF Core migrations run automatically at startup.

### Azure Storage (receipts only)

- Resource group: `rg-shora-prod-ne`
- Account: `stshoraprodne001`, container `receipts` (private)
- Rotate access keys if they were ever logged
- Set `Storage__ConnectionString` and `Storage__ReceiptContainer` on Railway

No Azure SQL or App Service is required for this path.

---

## B2 — Railway project

1. [railway.app](https://railway.app) → **New Project**.
2. Add a service with **Docker Image** source (see below).
3. **Settings → Networking → Generate Domain** → copy HTTPS URL (no trailing slash).
4. Note **Service ID** (Settings → General).

### Link GHCR image (for GitHub Actions deploy)

The Deploy workflow pushes `ghcr.io/<owner>/<repo>:production` and runs `railway redeploy`.

1. Railway service → **Settings → Source** → **Docker Image**.
2. Image: `ghcr.io/<github-owner>/Shora:production` (match GitHub owner/repo casing).
3. Grant Railway access to GHCR or make the package public if pulls fail.

---

## B4 — Railway variables

Set in Railway → service → **Variables** (same keys as [production-config.md](production-config.md)):

| Variable | Example |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Neon pooled PostgreSQL string |
| `Jwt__SigningKey` | 32+ char secret |
| `Frontend__BaseUrl` | `https://<railway-domain>` |
| `Cors__AllowedOrigins__0` | same as `Frontend__BaseUrl` |
| `AllowedHosts` | `<railway-hostname>` only |
| `Storage__ConnectionString` | Azure storage |
| `Storage__ReceiptContainer` | `receipts` |
| `Email__Host` | `smtp.sendgrid.net` |
| `Email__Port` | `587` |
| `Email__Username` | `apikey` |
| `Email__Password` | SendGrid API key |
| `Email__FromAddress` | verified sender |
| `Email__FromName` | `Shora` |
| `Google__ClientId` | optional |
| `AdminSeed__Email` / `AdminSeed__Password` | temporary; remove after first admin login |

---

## B5 — GitHub

| Item | Where | Value |
| --- | --- | --- |
| `RAILWAY_TOKEN` | Environment secret `production` | Railway → Account → Tokens |
| `RAILWAY_SERVICE_ID` | Repository variable | Service UUID |
| `PRODUCTION_URL` | Repository variable | `https://<railway-domain>` |
| Branch protection | `main` | Require CI Backend + Frontend |

---

## Google OAuth

1. [Google Cloud Console](https://console.cloud.google.com/apis/credentials) → OAuth Web client.
2. **Authorized JavaScript origins:** Railway HTTPS URL.
3. Set `Google__ClientId` on Railway and `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) before merge.

---

## Verify

1. Merge to `main` → **Deploy** workflow builds, pushes GHCR image, redeploys Railway.
2. `GET <PRODUCTION_URL>/api/v1/health` → healthy.
3. SPA at `/`, admin login, E2E booking flow.
4. Remove `AdminSeed__*` from Railway variables.

# Railway production prerequisites (Path B-lite)

Deploy Shora to **Railway** (API + Angular SPA in one container) with **Neon PostgreSQL** (database) and **Azure Blob Storage** (receipts only).

**Related:** [production-config.md](production-config.md) · [docs/README.md](README.md) (current status) · [`.github/workflows/README.md`](../.github/workflows/README.md)

## Architecture

| Component | Provider |
| --- | --- |
| Compute (API + SPA) | Railway |
| Database | Neon PostgreSQL (free tier) |
| Receipt blobs | Azure Storage (`stshoraprodne001` / `receipts`) |
| Email | SendGrid SMTP |
| Container registry | GitHub Container Registry (GHCR) |

**Production URL:** `https://shora-production.up.railway.app`

---

## Current provisioning (Shora)

| Item | Value | Status |
| --- | --- | --- |
| Neon project | `shora-prod` | Done |
| Azure RG | `rg-shora-prod-ne` (North Europe) | Done |
| Storage account | `stshoraprodne001`, container `receipts` | Done |
| Railway project | `Tansekak` (existing project — free plan project limit) | Done |
| Railway service | `Shora` | Done |
| Railway service ID | `f69a711c-b830-4a97-a269-fa5e2b6f4dc9` | Done |
| Public domain | `shora-production.up.railway.app` | Done |
| Docker image | `ghcr.io/mohamedmamdoouh/shora-consulting-platform:production` | Set on Railway |
| Railway variables | See [B4](#b4--railway-variables) | Done (operator) |
| GitHub deploy secrets | See [B5](#b5--github) | Operator |
| GHCR pull + live deploy | See [B3](#b3--ghcr-image-pull) + [Verify](#verify) | Pending |

> **Note:** Tansekak (another app) runs in a separate Railway project. Do not mix env vars or domains between the two apps.

---

## B1 — Neon + Azure storage

### Neon PostgreSQL

**Project:** `shora-prod` on [neon.tech](https://neon.tech).

1. Use the **pooled** connection string from the Neon dashboard (recommended for Railway restarts).
2. Ensure SSL is enabled — append `Ssl Mode=Require` if not already in the string.
3. Set on Railway: `ConnectionStrings__DefaultConnection`.

Example formats:

```text
Host=ep-xxx.region.aws.neon.tech;Database=neondb;Username=...;Password=...;Ssl Mode=Require
```

Or URI form (Npgsql accepts both):

```text
postgresql://user:password@ep-xxx-pooler.region.aws.neon.tech/neondb?sslmode=require
```

On first deploy, EF Core migrations run automatically at startup.

### Azure Storage (receipts only)

| Setting | Value |
| --- | --- |
| Resource group | `rg-shora-prod-ne` |
| Storage account | `stshoraprodne001` |
| Container | `receipts` (private) |

Set `Storage__ConnectionString` and `Storage__ReceiptContainer=receipts` on Railway.

Retrieve connection string locally (do not commit):

```powershell
az storage account show-connection-string `
  --name stshoraprodne001 `
  --resource-group rg-shora-prod-ne `
  --query connectionString `
  -o tsv
```

Rotate storage keys if they were ever logged. No Azure SQL or App Service is required for this path.

---

## B2 — Railway project

Shora runs as service **Shora** inside Railway project **Tansekak**.

| Setting | Value |
| --- | --- |
| Service name | `Shora` |
| Service ID | `f69a711c-b830-4a97-a269-fa5e2b6f4dc9` |
| Public URL | `https://shora-production.up.railway.app` |
| Source | Docker image (see below) |
| Health check path | `/api/v1/health` |

### Docker image (Railway source)

Railway → **Shora** → **Settings → Source → Docker Image**:

```text
ghcr.io/mohamedmamdoouh/shora-consulting-platform:production
```

The **Deploy** workflow builds and pushes this tag on every push to `main`. The workflow lowercases `${{ github.repository }}` before tagging — GHCR rejects uppercase path segments (e.g. `MohamedMamdoouh` → `mohamedmamdoouh`).

---

## B3 — GHCR image pull

Railway must be able to pull from GitHub Container Registry. The image is created by the **Deploy** workflow — run that at least once before expecting Railway to succeed.

### Option A — Public package (recommended for MVP)

1. GitHub → **Packages** → `shora-consulting-platform`
2. **Package settings** → **Change visibility** → **Public**
3. Redeploy on Railway

### Option B — Private package

1. GitHub → **Settings → Developer settings** → create a PAT with `read:packages`
2. Railway → **Shora** → **Settings → Registry** (or equivalent)
3. Registry: `ghcr.io`, username: GitHub username, password: PAT
4. Redeploy

### Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| `pull access denied` / `unauthorized` | Package private without Railway registry creds |
| `manifest unknown` / `not found` | Deploy workflow has not pushed the image yet |
| `invalid tag` / uppercase in image name | Use lowercase GHCR path (`mohamedmamdoouh/shora-consulting-platform`) |
| Deployment FAILED, no app logs | Image pull failed before container start |
| Health returns 404 | No healthy container behind Railway edge |

---

## B4 — Railway variables

Set in Railway → **Shora** → **Variables** (same keys as [production-config.md](production-config.md)).

Use `__` (double underscore) for nested JSON keys.

### Non-secret (example values for Shora)

| Variable | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Frontend__BaseUrl` | `https://shora-production.up.railway.app` |
| `Cors__AllowedOrigins__0` | `https://shora-production.up.railway.app` |
| `AllowedHosts` | `shora-production.up.railway.app` |
| `Storage__ReceiptContainer` | `receipts` |
| `Email__Host` | `smtp.sendgrid.net` |
| `Email__Port` | `587` |
| `Email__Username` | `apikey` (SendGrid constant — not a secret) |
| `Email__FromName` | `Shora` |

### Secrets (set in Railway only)

| Variable | Source |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Neon pooled connection string |
| `Jwt__SigningKey` | Random string, 32+ characters |
| `Storage__ConnectionString` | Azure CLI or Portal |
| `Email__Password` | SendGrid API key |
| `Email__FromAddress` | SendGrid verified sender |
| `AdminSeed__Email` / `AdminSeed__Password` | One-time admin bootstrap — **remove after first login** |
| `Google__ClientId` | Optional |

Production startup validation requires JWT, Neon, storage, email (`Host` + `FromAddress`), frontend URL, and CORS origin — see [production-config.md](production-config.md).

---

## B5 — GitHub

Configure in [MohamedMamdoouh/shora-consulting-platform](https://github.com/MohamedMamdoouh/shora-consulting-platform) → **Settings**.

| Item | Where | Shora value |
| --- | --- | --- |
| `RAILWAY_SERVICE_ID` | Repository **variable** (not secret) | `f69a711c-b830-4a97-a269-fa5e2b6f4dc9` |
| `PRODUCTION_URL` | Repository **variable** | `https://shora-production.up.railway.app` |
| `RAILWAY_TOKEN` | Environment **secret** on `production` | **Project token** for the `production` environment — Railway project **shora** → **Settings** → **Tokens** → Create token. Do **not** use an account token here. |
| Branch protection | `main` | Require CI Backend + Frontend (recommended) |

Optional repository variable: `DEPLOY_ENVIRONMENT` (defaults to `production`).

The Deploy job fails explicitly if any of `RAILWAY_SERVICE_ID`, `PRODUCTION_URL`, or `RAILWAY_TOKEN` is missing.

### Troubleshooting `RAILWAY_TOKEN`

| Symptom | Fix |
| --- | --- |
| `Invalid RAILWAY_TOKEN` | Secret is an **account** token or expired. Create a **project token** (project → Settings → Tokens → `production`) and update the GitHub secret. |
| `No linked project found` | Same fix — account tokens need `railway link`; project tokens do not. Use a project token with `RAILWAY_TOKEN`. |
| Token set but still invalid | Must be an **environment secret** on GitHub Environment **`production`**, not a repository secret — `deploy.yml` uses `environment: production` |
| Deploy skipped / empty token | Confirm Environment `production` exists under **Settings → Environments** |

---

## Manual redeploy

Use when the Deploy workflow pushed a new image but Railway is still running an old build, or after fixing GHCR pull access.

**Railway dashboard:** Project **Tansekak** → service **Shora** → **Deployments** → **Redeploy** (latest image).

**Railway CLI** (with project linked):

```powershell
railway redeploy --service f69a711c-b830-4a97-a269-fa5e2b6f4dc9
```

**GitHub Actions:** Re-run the **Deploy** workflow on `main` after fixing secrets — it builds, pushes GHCR, and calls `railway redeploy`.

---

## Google OAuth (optional)

1. [Google Cloud Console](https://console.cloud.google.com/apis/credentials) → OAuth Web client.
2. **Authorized JavaScript origins:** `https://shora-production.up.railway.app`
3. Set `Google__ClientId` on Railway and `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) before merge.

---

## Verify

1. **Deploy** workflow green on GitHub Actions (build → push GHCR → `railway redeploy` → smoke test).
2. Railway deployment status **Success** (not FAILED).
3. `GET https://shora-production.up.railway.app/api/v1/health` → `healthy`.
4. SPA loads at `/` and `/about`.
5. Log in as admin at `/auth/login` (AdminSeed account).
6. **Admin → Settings** — set real WhatsApp, Vodafone Cash, and InstaPay values.
7. Optional E2E: client signup → verification email → book → receipt upload → admin approve.
8. **Remove** `AdminSeed__Email` and `AdminSeed__Password` from Railway variables.

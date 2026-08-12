# Production deployment

Deploy Shora to **Railway** (API + Angular SPA in one container) with **Neon PostgreSQL** (database) and **Azure Blob Storage** (receipt images only). The **Deploy** workflow builds a Docker image, pushes it to GitHub Container Registry (GHCR), and redeploys Railway on every push to `main`.

Non-secret config structure lives in [`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json). **Never commit real credentials** — set values as Railway environment variables.

Use double-underscore nesting for nested JSON (e.g. `Jwt__SigningKey` → `Jwt:SigningKey`).

**Related:** [`.github/workflows/README.md`](../.github/workflows/README.md) (CI/CD behavior)

## Overview

| Component | Provider |
| --- | --- |
| Compute (API + SPA) | Railway |
| Database | Neon PostgreSQL |
| Receipt blobs | Azure Blob Storage (private container) |
| Email | Resend (HTTPS API) |
| Container registry | GitHub Container Registry (GHCR) |

One process hosts the API, Angular static files (`wwwroot`), in-process background jobs, and in-memory cache. There is no load balancer, horizontal scaling, or distributed cache.

## 1. Infrastructure prerequisites

### Neon PostgreSQL

1. Create a project on [neon.tech](https://neon.tech).
2. Copy the **pooled** connection string from the Neon dashboard (recommended for Railway restarts).
3. Ensure SSL is enabled — append `Ssl Mode=Require` if not already in the string.

**Connection string format on Railway:** prefer Npgsql **key-value** format:

```text
Host=ep-xxx-pooler.region.aws.neon.tech;Database=neondb;Username=...;Password=...;Ssl Mode=Require
```

URI form (`postgresql://...?sslmode=require`) works locally but Railway can truncate values at `=`, leaving a broken `?sslmode` suffix.

On first deploy, EF Core migrations run automatically at startup.

### Azure Blob Storage (receipts only)

1. Create a storage account and a **private** blob container (e.g. `receipts`).
2. Set `Storage__ConnectionString` and `Storage__ReceiptContainer` on Railway.

Retrieve the connection string locally (do not commit):

```powershell
az storage account show-connection-string `
  --name <storage-account-name> `
  --resource-group <resource-group> `
  --query connectionString `
  -o tsv
```

Rotate storage keys if they were ever logged.

## 2. Railway service

1. Create a Railway project and service.
2. Assign a public domain (e.g. `https://<your-app>.up.railway.app`).
3. Set the health check path to `/api/v1/health`.
4. Configure the service source as a **Docker image**:

```text
ghcr.io/<github-owner>/<github-repo>:production
```

The **Deploy** workflow builds and pushes this tag on every push to `main`. The workflow lowercases `${{ github.repository }}` before tagging — GHCR rejects uppercase path segments.

## 3. GHCR image pull

Railway must be able to pull from GitHub Container Registry. Run the **Deploy** workflow at least once before expecting Railway to succeed.

### Option A — Public package (recommended for MVP)

1. GitHub → **Packages** → your repo package
2. **Package settings** → **Change visibility** → **Public**
3. Redeploy on Railway

### Option B — Private package

1. GitHub → **Settings → Developer settings** → create a PAT with `read:packages`
2. Railway → service **Settings → Registry**
3. Registry: `ghcr.io`, username: GitHub username, password: PAT
4. Redeploy

### Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| `pull access denied` / `unauthorized` | Package private without Railway registry creds |
| `manifest unknown` / `not found` | Deploy workflow has not pushed the image yet |
| `invalid tag` / uppercase in image name | Use lowercase GHCR path |
| Deployment FAILED, no app logs | Image pull failed before container start |
| Health returns 404 | No healthy container behind Railway edge |

## 4. Environment variables

Set in Railway → service → **Variables**. Use `__` (double underscore) for nested JSON keys.

### Required (startup fails without these)

| Setting | Variable name | Notes |
| --- | --- | --- |
| Environment | `ASPNETCORE_ENVIRONMENT` | `Production` |
| Database | `ConnectionStrings__DefaultConnection` | Neon pooled connection string |
| JWT signing key | `Jwt__SigningKey` | Min 32 chars; strong random |
| Production URL | `Frontend__BaseUrl` | e.g. `https://<your-production-url>` — no trailing slash |
| CORS origin | `Cors__AllowedOrigins__0` | **Same URL** as `Frontend__BaseUrl` (same-site auth) |
| Blob storage | `Storage__ConnectionString` | Azure Storage connection string |
| Receipt container | `Storage__ReceiptContainer` | Private container name (default `receipts`) |
| Resend API key | `Email__ApiKey` | From [Resend dashboard](https://resend.com/api-keys) |
| From address | `Email__FromAddress` | Verified sender at Resend |

Also set `AllowedHosts` to the hostname only (e.g. `<your-app>.up.railway.app` — no `https://`). Include `healthcheck.railway.app` as well — Railway sends deploy healthchecks from that hostname, and omitting it causes HTTP 400 responses and failed deploys.

[`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json) may ship with example host values. **Railway variables override** JSON when set — ensure `Frontend__BaseUrl` and `Cors__AllowedOrigins__0` match your production URL, not `http://localhost:4200`, or startup validation fails.

### Secrets (Railway only — never in git)

| Variable | Source |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Neon pooled connection string |
| `Jwt__SigningKey` | Random string, 32+ characters |
| `Storage__ConnectionString` | Azure CLI or Portal |
| `Email__ApiKey` | Resend API key (`re_...`) |
| `Email__FromAddress` | Resend verified sender domain |
| `AdminSeed__Email` / `AdminSeed__Password` | One-time admin bootstrap — **remove after first login** |
| `Google__ClientId` | Optional |

### Common non-secret values

| Variable | Example |
| --- | --- |
| `Email__FromName` | `Shora` |

### CORS pitfall

If `Cors__AllowedOrigins__0` on Railway is still `http://localhost:4200`, it overrides baked production config and the app fails `ValidateOnStart` with a CORS validation error. Set it to the same HTTPS URL as `Frontend__BaseUrl`.

Refresh cookies use `Secure=true` and `SameSite=Strict` outside Development.

### Email (required for sending mail)

Shora uses the **Resend HTTPS API** for all production email (auth + transactional outbox). This works on all Railway plans.

Set on Railway:

| Setting | Variable name | Notes |
| --- | --- | --- |
| API key | `Email__ApiKey` | From Resend dashboard — **secret** |
| From address | `Email__FromAddress` | Must use a domain verified in Resend |
| From display name | `Email__FromName` | Optional; defaults to `Shora` |

After changing email variables, **redeploy** the service.

### Admin bootstrap (first production admin)

There is no public admin registration.

1. Before first deploy, set `AdminSeed__Email` and `AdminSeed__Password` on Railway.
2. On startup, [`DatabaseSeeder`](../src/backend/Shora.Infrastructure/Data/DatabaseSeeder.cs) creates the Admin user if it does not exist.
3. Log in at `/auth/login`, confirm admin dashboard access.
4. **Remove** `AdminSeed__Email` and `AdminSeed__Password` from Railway variables.

If `AdminSeed` is omitted, the app starts but **no admin exists** — receipt approval and admin settings are unavailable.

### Payment defaults (first run)

On first startup, the singleton `Settings` row is seeded from `Seed:*` in [`appsettings.json`](../src/backend/Shora.Api/appsettings.json) if no row exists yet. Defaults are **placeholder test values** — update via `/admin/settings` after first login, or set `Seed__*` env vars **before the first app startup**:

| Setting | Variable name |
| --- | --- |
| WhatsApp | `Seed__ConsultantWhatsAppNumber` |
| Vodafone Cash | `Seed__VodafoneCashNumber` |
| InstaPay | `Seed__InstaPayHandle` |
| Optional payment note | `Seed__PaymentInstructions` |

### Optional tuning

Inherits from base `appsettings.json` unless overridden:

- `BackgroundJobs`, `OpsMonitoring`, `RateLimiting`, `Cache`, `Booking`, `ReceiptUpload`, `Brand`
- `Jwt__Issuer`, `Jwt__Audience`, token lifetimes

### Frontend (build-time, not Railway)

| File | Purpose |
| --- | --- |
| [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) | `googleClientId` for Google button; `apiBaseUrl: '/api/v1'` is correct for same-site deploy |
| [`angular.json`](../src/frontend/angular.json) | Production build uses `fileReplacements` to swap in `environment.production.ts` |

Set `googleClientId` in `environment.production.ts` **before merge to `main`** so the Deploy workflow bakes it into the SPA bundle.

## 5. GitHub secrets and variables

Configure in your GitHub repo → **Settings**.

| Item | Where | Value |
| --- | --- | --- |
| `RAILWAY_SERVICE_ID` | Repository **variable** (not secret) | Railway service ID from dashboard |
| `PRODUCTION_URL` | Repository **variable** | e.g. `https://<your-production-url>` |
| `RAILWAY_TOKEN` | Environment **secret** on `production` | **Project token** for the production environment — Railway project → **Settings** → **Tokens**. Do **not** use an account token. |

Optional repository variable: `DEPLOY_ENVIRONMENT` (defaults to `production`).

The Deploy job fails explicitly if any of `RAILWAY_SERVICE_ID`, `PRODUCTION_URL`, or `RAILWAY_TOKEN` is missing.

### Troubleshooting `RAILWAY_TOKEN`

| Symptom | Fix |
| --- | --- |
| `Invalid RAILWAY_TOKEN` | Secret is an **account** token or expired. Create a **project token** and update the GitHub secret. |
| `No linked project found` | Account tokens need `railway link`; this workflow uses **project tokens** with `RAILWAY_TOKEN` only. |
| Token set but still invalid | Must be an **environment secret** on GitHub Environment **`production`**, not a repository secret — `deploy.yml` uses `environment: production` |
| Deploy skipped / empty token | Confirm Environment `production` exists under **Settings → Environments** |

Enable **branch protection** on `main` so CI passes before merge (recommended).

## 6. Google OAuth (optional)

Only needed for the Google sign-in button on the login page.

1. [Google Cloud Console](https://console.cloud.google.com/) → **APIs & Services → Credentials** → **Create credentials → OAuth client ID**.
2. Application type: **Web application**.
3. **Authorized JavaScript origins:** your production HTTPS URL (same as `Frontend__BaseUrl`).
4. **Authorized redirect URIs:** not required for the current Google Identity Services button flow (ID token only).
5. Copy the **Client ID** into Railway `Google__ClientId` and `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts).
6. Redeploy (merge to `main`) after changing the frontend file.

`Google__ClientSecret` is **not used** by the current ID-token flow — only `Google__ClientId` is validated server-side.

First-time Google users sign in from the **login** page (not signup).

## 7. Custom domain (optional)

1. Railway → **Settings → Networking** → add custom domain → complete DNS (CNAME to Railway-provided target).
2. Update **all** URL-dependent settings to the new HTTPS origin (no trailing slash):
   - `Frontend__BaseUrl`
   - `Cors__AllowedOrigins__0`
   - Google Cloud **Authorized JavaScript origins** (if using Google sign-in)
   - Rebuild frontend if origins change (same `googleClientId`; origins must include the new domain)

Optional: add **`shora.dev`** as a second custom domain on the same Railway service so RFC 7807 `type` URIs (`https://shora.dev/errors/{code}`) resolve to the live error reference pages without changing API responses.

## 8. Manual redeploy

Use when the Deploy workflow pushed a new image but Railway is still running an old build, or after fixing GHCR pull access.

- **Railway dashboard:** service → **Deployments** → **Redeploy** (latest image).
- **Railway CLI:** `railway redeploy --service <service-id>`
- **GitHub Actions:** Re-run the **Deploy** workflow on `main` after fixing secrets.

## 9. Verify

1. All required Railway variables saved (no secrets in git).
2. Merge to `main` — **Deploy** runs automatically ([`deploy.yml`](../.github/workflows/deploy.yml)).
3. Deploy workflow green (build → push GHCR → `railway redeploy` → smoke test).
4. Railway deployment status **Success**.
5. `GET https://<your-production-url>/api/v1/health` → `healthy`.
6. SPA loads at `/` and `/about`.
7. Log in as admin at `/auth/login` (AdminSeed account).
8. **Admin → Settings** — set real WhatsApp, Vodafone Cash, and InstaPay values.
9. Optional E2E: client signup → verification email → book → receipt upload → admin approve.
10. **Remove** `AdminSeed__Email` and `AdminSeed__Password` from Railway variables.

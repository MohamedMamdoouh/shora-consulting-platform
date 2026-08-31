# Production deployment

Deploy Shora to **Render** (API + Angular SPA in one container) with **Neon PostgreSQL** (database) and **Azure Blob Storage** (receipt images only). Render builds from the Git repo using the multi-stage [`Dockerfile`](../Dockerfile) on every push to `main` (auto-deploy).

Non-secret config structure lives in [`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json). **Never commit real credentials** — set values as Render environment variables.

Use double-underscore nesting for nested JSON (e.g. `Jwt__SigningKey` → `Jwt:SigningKey`).

**Related:** [`.github/workflows/README.md`](../.github/workflows/README.md) (CI behavior)

## Overview

| Component           | Provider                               |
| ------------------- | -------------------------------------- |
| Compute (API + SPA) | Render (Docker build from Git)         |
| Database            | Neon PostgreSQL                        |
| Receipt blobs       | Azure Blob Storage (private container) |
| Email               | Brevo (HTTPS API)                      |
| CI (tests only)     | GitHub Actions (`ci.yml`)              |

One process hosts the API, Angular static files (`wwwroot`), in-process background jobs, and in-memory cache. There is no load balancer, horizontal scaling, or distributed cache.

## 1. Infrastructure prerequisites

### Neon PostgreSQL

1. Create a project on [neon.tech](https://neon.tech).
2. Copy the **pooled** connection string from the Neon dashboard (recommended for Render restarts).
3. Ensure SSL is enabled — append `Ssl Mode=Require` if not already in the string.

**Connection string format on Render:** prefer Npgsql **key-value** format:

```text
Host=ep-xxx-pooler.region.aws.neon.tech;Database=neondb;Username=...;Password=...;Ssl Mode=Require
```

URI form (`postgresql://...?sslmode=require`) works locally but some platforms can truncate values at `=`, leaving a broken `?sslmode` suffix.

On first deploy, EF Core migrations run automatically at startup.

### Azure Blob Storage (receipts only)

1. Create a storage account and a **private** blob container (e.g. `receipts`).
2. Set `Storage__ConnectionString` and `Storage__ReceiptContainer` on Render.

Retrieve the connection string locally (do not commit):

```powershell
az storage account show-connection-string `
  --name <storage-account-name> `
  --resource-group <resource-group> `
  --query connectionString `
  -o tsv
```

Rotate storage keys if they were ever logged.

## 2. Render web service (Git + Docker)

1. Render Dashboard → **New +** → **Web Service**
2. Connect repository: `MohamedMamdoouh/shora-consulting-platform`
3. **Language:** Docker
4. **Branch:** `main`
5. **Auto-Deploy:** Yes
6. **Name:** `shora` (or your preference)
7. **Health Check Path:** `/api/v1/health`
8. **Region:** your choice (e.g. Frankfurt)
9. **Dockerfile Path:** `Dockerfile`
10. **Docker Build Context Directory:** `.` (repo root)

Render reads [`Dockerfile`](../Dockerfile) from the repo root. The multi-stage build:

1. Builds the Angular frontend (`npm ci` + `npm run build`)
2. Publishes the .NET API with static files in `wwwroot`
3. Runs the final image on `mcr.microsoft.com/dotnet/aspnet:10.0`

**Deploy trigger:** merge/push to `main` → Render builds and deploys automatically. No GitHub deploy workflow or container registry is required.

### Build troubleshooting

| Symptom | Likely cause |
| ------- | ------------ |
| `Cannot find module '@contracts/...'` | Frontend build stage missing `src/contracts` — ensure [`Dockerfile`](../Dockerfile) copies `src/contracts/` |
| Docker build fails at `npm ci` | Frontend dependency or lockfile issue — check Render build logs |
| Docker build fails at `dotnet publish` | Backend compile error — run `dotnet build` locally |
| `COPY` path not found | `dist/shora-web/browser` missing — frontend build stage failed |
| Health check timeout | App not listening on port 8080 — confirm `ASPNETCORE_HTTP_PORTS=8080` |
| Startup validation error | `Frontend__BaseUrl` / CORS mismatch — see env vars below |

## 3. Environment variables

Set in Render Dashboard → **shora** → **Environment**. Use `__` (double underscore) for nested JSON keys.

### Required (startup fails without these)

| Setting           | Variable name                          | Notes                                                                 |
| ----------------- | -------------------------------------- | --------------------------------------------------------------------- |
| Environment       | `ASPNETCORE_ENVIRONMENT`               | `Production`                                                          |
| HTTP port         | `ASPNETCORE_HTTP_PORTS`                | `8080`                                                                |
| Database          | `ConnectionStrings__DefaultConnection` | Neon pooled connection string                                         |
| JWT signing key   | `Jwt__SigningKey`                      | Min 32 chars; strong random                                           |
| Production URL    | `Frontend__BaseUrl`                    | e.g. `https://shora.onrender.com` — no trailing slash                 |
| CORS origin       | `Cors__AllowedOrigins__0`              | **Same URL** as `Frontend__BaseUrl` (same-site auth)                  |
| Allowed hosts     | `AllowedHosts`                         | Hostname only, e.g. `shora.onrender.com` (no `https://`)              |
| Blob storage      | `Storage__ConnectionString`            | Azure Storage connection string                                       |
| Receipt container | `Storage__ReceiptContainer`            | Private container name (default `receipts`)                           |
| Brevo API key     | `Email__ApiKey`                        | From [Brevo dashboard](https://app.brevo.com) → SMTP & API → API keys |
| From address      | `Email__FromAddress`                   | Verified sender in Brevo (e.g. your Gmail)                            |

[`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json) may ship with example host values. **Render variables override** JSON when set — ensure `Frontend__BaseUrl` and `Cors__AllowedOrigins__0` match your production URL, not `http://localhost:4200`, or startup validation fails.

### Secrets (Render only — never in git)

| Variable                                   | Source                                                  |
| ------------------------------------------ | ------------------------------------------------------- |
| `ConnectionStrings__DefaultConnection`     | Neon pooled connection string                           |
| `Jwt__SigningKey`                          | Random string, 32+ characters                           |
| `Storage__ConnectionString`                | Azure CLI or Portal                                     |
| `Email__ApiKey`                            | Brevo API key (`xkeysib-...`)                           |
| `Email__FromAddress`                       | Brevo verified sender (must match dashboard exactly)    |
| `AdminSeed__Email` / `AdminSeed__Password` | One-time admin bootstrap — **remove after first login** |

### Common non-secret values

| Variable          | Example |
| ----------------- | ------- |
| `Email__FromName` | `Shora` |

### CORS pitfall

If `Cors__AllowedOrigins__0` on Render is still `http://localhost:4200`, it overrides baked production config and the app fails `ValidateOnStart` with a CORS validation error. Set it to the same HTTPS URL as `Frontend__BaseUrl`.

### Production URL validation (`ValidateOnStart`)

In Production, [`FrontendOptionsValidator`](../src/backend/Shora.Application/Options/FrontendOptionsValidator.cs) and [`CorsOptionsValidator`](../src/backend/Shora.Application/Options/CorsOptionsValidator.cs) run at startup and **fail fast** when:

| Check                                   | Applies to                                                                                                                        |
| --------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| Must be a valid absolute **HTTPS** URL  | `Frontend__BaseUrl`, each `Cors__AllowedOrigins__*` entry                                                                         |
| Must not contain `YOUR_PRODUCTION_HOST` | Same (placeholder host in [`ProductionConfigValidation`](../src/backend/Shora.Application/Options/ProductionConfigValidation.cs)) |
| At least one CORS origin required       | `Cors__AllowedOrigins`                                                                                                            |

HTTP URLs (including `http://localhost:4200`) are rejected in Production. Render variables override [`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json) — ensure both URL settings match your live HTTPS origin.

Refresh cookies use `Secure=true` and `SameSite=Strict` outside Development.

### Email (required for sending mail)

Shora sends auth and transactional emails via the **Brevo HTTPS API**. Gmail SMTP is not used in production.

1. Sign up at [brevo.com](https://www.brevo.com) (free tier: 300 emails/day, no credit card).
2. **Senders, Domains & Dedicated IPs → Senders → Add sender** — use your Gmail address and complete Brevo’s verification email/code.
3. **SMTP & API → API keys** — create a key (`xkeysib-...`).
4. Set on Render:

| Setting           | Variable name        | Notes                                                    |
| ----------------- | -------------------- | -------------------------------------------------------- |
| API key           | `Email__ApiKey`      | Brevo API key — **secret**                               |
| From address      | `Email__FromAddress` | Must match the verified sender exactly (e.g. your Gmail) |
| From display name | `Email__FromName`    | Optional                                                 |

Deliverability from `@gmail.com` is weaker than a custom domain; check spam on first send. You can later authenticate your own domain in Brevo for better deliverability.

After changing email variables, **redeploy** the service.

### Admin bootstrap (first production admin)

There is no public admin registration.

1. Before first deploy, set `AdminSeed__Email` and `AdminSeed__Password` on Render.
2. On startup, [`DatabaseSeeder`](../src/backend/Shora.Infrastructure/Data/DatabaseSeeder.cs) creates the Admin user if it does not exist.
3. Log in at `/auth/login`, confirm admin dashboard access.
4. **Remove** `AdminSeed__Email` and `AdminSeed__Password` from Render variables.

If `AdminSeed` is omitted, the app starts but **no admin exists** — receipt approval and admin settings are unavailable.

### Payment defaults (first run)

On first startup, the singleton `Settings` row is seeded from `Seed:*` in [`appsettings.json`](../src/backend/Shora.Api/appsettings.json) if no row exists yet. Defaults are **placeholder test values** — update via `/admin/settings` after first login, or set `Seed__*` env vars **before the first app startup**:

| Setting               | Variable name                    |
| --------------------- | -------------------------------- |
| WhatsApp              | `Seed__ConsultantWhatsAppNumber` |
| Vodafone Cash         | `Seed__VodafoneCashNumber`       |
| InstaPay              | `Seed__InstaPayHandle`           |
| Optional payment note | `Seed__PaymentInstructions`      |

### Optional tuning

Inherits from base `appsettings.json` unless overridden:

- `BackgroundJobs`, `OpsMonitoring`, `RateLimiting`, `Cache`, `Booking`, `ReceiptUpload`, `Brand`
- `Jwt__Issuer`, `Jwt__Audience`, token lifetimes

### Frontend (build-time)

The Angular production build runs inside the Docker build stage. [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) uses `apiBaseUrl: '/api/v1'` (correct for same-site deploy). No separate frontend deploy step is required.

## 4. GitHub (CI only)

Production deploys are handled by Render auto-deploy on push to `main`. GitHub Actions runs **CI only** ([`ci.yml`](../.github/workflows/ci.yml)) — backend and frontend tests on PRs and pushes.

Optional repository variable for documentation or scripts:

| Item             | Where               | Value                             |
| ---------------- | ------------------- | --------------------------------- |
| `PRODUCTION_URL` | Repository variable | e.g. `https://shora.onrender.com` |

No deploy secrets are required on GitHub. Remove legacy secrets if present: `RENDER_DEPLOY_HOOK_URL`, `RAILWAY_TOKEN`, `RAILWAY_SERVICE_ID`.

Enable **branch protection** on `main` so CI passes before merge (recommended).

## 5. Custom domain (optional)

1. Render → **shora** → **Settings → Custom Domains** → add domain → complete DNS.
2. Update **all** URL-dependent settings to the new HTTPS origin (no trailing slash):
   - `Frontend__BaseUrl`
   - `Cors__AllowedOrigins__0`
   - `AllowedHosts`
3. Push to `main` to trigger a rebuild if frontend origins are baked at build time.

## 6. Manual redeploy

- **Render dashboard:** service → **Manual Deploy** → **Deploy latest commit**
- **Git:** push to `main` (auto-deploy when enabled)

## 7. Verify

1. All required Render variables saved (no secrets in git).
2. Merge to `main` — Render build starts automatically.
3. Render deployment status **Live** (check build logs for Docker stages).
4. `GET https://<your-production-url>/api/v1/health` → `healthy`.
5. SPA loads at `/` and `/about`.
6. Log in as admin at `/auth/login` (AdminSeed account).
7. **Admin → Settings** — set real WhatsApp, Vodafone Cash, and InstaPay values.
8. Optional E2E: client signup → verification email → book → receipt upload → admin approve.
9. **Remove** `AdminSeed__Email` and `AdminSeed__Password` from Render variables.

## 8. Free tier notes

- Render free web services spin down after **15 minutes** of inactivity (cold starts on next request).
- Docker builds on free tier can be slow (Node + .NET in one image).
- Ephemeral filesystem — receipt images use Azure Blob (unchanged).

## 9. Local Docker build (optional)

Test the production image locally before pushing:

```powershell
docker build -t shora:local .
docker run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Production -e ConnectionStrings__DefaultConnection="..." shora:local
```

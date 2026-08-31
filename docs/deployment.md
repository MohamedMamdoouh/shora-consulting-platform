# Production deployment

Deploy Shora to **Render** (API + Angular SPA in one container) with **Neon PostgreSQL** (database) and **Azure Blob Storage** (receipt images only). The **Deploy** workflow builds a Docker image, pushes it to GitHub Container Registry (GHCR), and triggers a Render deploy hook on every push to `main`.

Non-secret config structure lives in [`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json). **Never commit real credentials** — set values as Render environment variables.

Use double-underscore nesting for nested JSON (e.g. `Jwt__SigningKey` → `Jwt:SigningKey`).

**Related:** [`.github/workflows/README.md`](../.github/workflows/README.md) (CI/CD behavior)

## Overview

| Component           | Provider                               |
| ------------------- | -------------------------------------- |
| Compute (API + SPA) | Render                                 |
| Database            | Neon PostgreSQL                        |
| Receipt blobs       | Azure Blob Storage (private container) |
| Email               | Brevo (HTTPS API)                      |
| Container registry  | GitHub Container Registry (GHCR)       |

One process hosts the API, Angular static files (`wwwroot`), in-process background jobs, and in-memory cache. There is no load balancer, horizontal scaling, or distributed cache.

Infrastructure is defined in [`render.yaml`](../render.yaml) (Render Blueprint).

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

## 2. Render service (Blueprint)

1. Ensure [`render.yaml`](../render.yaml) is committed on `main`.
2. Open the Blueprint deeplink (replace repo if needed):

   ```text
   https://dashboard.render.com/blueprint/new?repo=https://github.com/MohamedMamdoouh/shora-consulting-platform
   ```

3. Click **Apply** to create the `shora` web service.
4. Note the assigned URL (e.g. `https://shora.onrender.com`).
5. Health check path is `/api/v1/health` (configured in `render.yaml`).

The service pulls a prebuilt image:

```text
ghcr.io/mohamedmamdoouh/shora-consulting-platform:production
```

The **Deploy** workflow builds and pushes this tag on every push to `main`, then calls the Render deploy hook. Render does **not** auto-redeploy when a mutable tag (`:production`) is updated — the hook is required.

### Deploy hook

After the service exists:

1. Render Dashboard → **shora** → **Settings** → **Deploy Hook**
2. Copy the hook URL
3. Add as GitHub Environment secret `RENDER_DEPLOY_HOOK_URL` on `production`

## 3. GHCR image pull

Render must be able to pull from GitHub Container Registry. Run the **Deploy** workflow at least once before expecting Render to succeed.

### Option A — Public package (recommended for MVP)

1. GitHub → **Packages** → your repo package
2. **Package settings** → **Change visibility** → **Public**
3. Remove `registryCredential` from `render.yaml` if you use this option
4. Redeploy on Render

### Option B — Private package

1. GitHub → **Settings → Developer settings** → create a PAT with `read:packages`
2. Render Dashboard → **Registry Credentials** → create credential named `ghcr-shora`
3. Username: GitHub username; Password: PAT
4. `render.yaml` references this via `registryCredential.fromRegistryCreds.name: ghcr-shora`
5. Redeploy

### Troubleshooting

| Symptom                                 | Likely cause                                  |
| --------------------------------------- | --------------------------------------------- |
| `pull access denied` / `unauthorized`   | Package private without Render registry creds |
| `manifest unknown` / `not found`        | Deploy workflow has not pushed the image yet  |
| `invalid tag` / uppercase in image name | Use lowercase GHCR path                       |
| Deployment FAILED, no app logs          | Image pull failed before container start      |
| Health returns 404                      | No healthy container behind Render edge       |

## 4. Environment variables

Set in Render Dashboard → **shora** → **Environment**, or via Render MCP `update_environment_variables`. Use `__` (double underscore) for nested JSON keys.

### Required (startup fails without these)

| Setting           | Variable name                          | Notes                                                                 |
| ----------------- | -------------------------------------- | --------------------------------------------------------------------- |
| Environment       | `ASPNETCORE_ENVIRONMENT`               | `Production` (set in `render.yaml`)                                 |
| HTTP port         | `ASPNETCORE_HTTP_PORTS`                | `8080` (set in `render.yaml`)                                       |
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

### Frontend (build-time, not Render)

| File                                                                                      | Purpose                                                                         |
| ----------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) | `apiBaseUrl: '/api/v1'` is correct for same-site deploy                         |
| [`angular.json`](../src/frontend/angular.json)                                            | Production build uses `fileReplacements` to swap in `environment.production.ts` |

## 5. GitHub secrets and variables

Configure in your GitHub repo → **Settings**.

| Item                     | Where                                  | Value                                                                 |
| ------------------------ | -------------------------------------- | --------------------------------------------------------------------- |
| `RENDER_DEPLOY_HOOK_URL` | Environment **secret** on `production` | Deploy hook URL from Render service Settings                          |
| `PRODUCTION_URL`         | Repository **variable**                | e.g. `https://shora.onrender.com`                                     |

Optional repository variable: `DEPLOY_ENVIRONMENT` (defaults to `production`).

The Deploy job fails explicitly if `PRODUCTION_URL` or `RENDER_DEPLOY_HOOK_URL` is missing.

Remove legacy Railway configuration after cutover: `RAILWAY_SERVICE_ID`, `RAILWAY_TOKEN`.

Enable **branch protection** on `main` so CI passes before merge (recommended).

## 6. Custom domain (optional)

1. Render → **shora** → **Settings → Custom Domains** → add domain → complete DNS.
2. Update **all** URL-dependent settings to the new HTTPS origin (no trailing slash):
   - `Frontend__BaseUrl`
   - `Cors__AllowedOrigins__0`
   - `AllowedHosts`

Rebuild frontend if origins change.

## 7. Manual redeploy

Use when the Deploy workflow pushed a new image but Render is still running an old build, or after fixing GHCR pull access.

- **Render dashboard:** service → **Manual Deploy** → **Deploy latest image**.
- **GitHub Actions:** Re-run the **Deploy** workflow on `main` after fixing secrets (triggers deploy hook).
- **Render MCP:** `trigger_deploy(serviceId: "...")`.

## 8. Verify

1. All required Render variables saved (no secrets in git).
2. Merge to `main` — **Deploy** runs automatically ([`deploy.yml`](../.github/workflows/deploy.yml)).
3. Deploy workflow green (build → push GHCR → Render deploy hook).
4. Render deployment status **Live**.
5. `GET https://<your-production-url>/api/v1/health` → `healthy`.
6. SPA loads at `/` and `/about`.
7. Log in as admin at `/auth/login` (AdminSeed account).
8. **Admin → Settings** — set real WhatsApp, Vodafone Cash, and InstaPay values.
9. Optional E2E: client signup → verification email → book → receipt upload → admin approve.
10. **Remove** `AdminSeed__Email` and `AdminSeed__Password` from Render variables.

## 9. Free tier notes

- Render free web services spin down after **15 minutes** of inactivity (cold starts on next request).
- Ephemeral filesystem — receipt images use Azure Blob (unchanged).

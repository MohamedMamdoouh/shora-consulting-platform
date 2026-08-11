# Production configuration (spec 09.6)

Non-secret **structure** lives in [`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json). **Never commit real credentials** — set values as environment variables on your host.

**Active deploy path (Path B-lite):** [Railway](https://railway.app) variables — see [railway-prerequisites.md](railway-prerequisites.md).  
**Legacy path:** Azure App Service application settings — see [azure-prerequisites.md](azure-prerequisites.md).

**Current production host:** `https://shora-production.up.railway.app` — provisioning status in [docs/README.md](README.md).

Use double-underscore nesting for nested JSON (e.g. `Jwt__SigningKey` → `Jwt:SigningKey`).

## Where to set values

| Host | UI location |
| --- | --- |
| **Railway** (current) | Service → **Variables** |
| **Azure App Service** (legacy) | **Configuration → Application settings** |

Variable names are identical on both hosts.

## Required settings (startup)

These must be set on Railway or the app **fails to start** (`ValidateOnStart` in Production):

| Setting | Variable name | Notes |
| --- | --- | --- |
| Environment | `ASPNETCORE_ENVIRONMENT` | `Production` |
| Database | `ConnectionStrings__DefaultConnection` | Neon PostgreSQL (pooled recommended) |
| JWT signing key | `Jwt__SigningKey` | Min 32 chars; strong random (spec 02) |
| Production URL | `Frontend__BaseUrl` | e.g. `https://shora-production.up.railway.app` — no trailing slash |
| CORS origin | `Cors__AllowedOrigins__0` | **Same URL** as `Frontend__BaseUrl` (same-site auth) |
| Blob storage | `Storage__ConnectionString` | Azure Storage account (`stshoraprodne001`) |
| Receipt container | `Storage__ReceiptContainer` | Private container name (default `receipts`) |
| SMTP host | `Email__Host` | e.g. `smtp.sendgrid.net` |
| From address | `Email__FromAddress` | Verified sender at your provider |

Also set `AllowedHosts` to the hostname only (e.g. `shora-production.up.railway.app` — no `https://`).

If `Frontend__BaseUrl` / `Cors__AllowedOrigins__0` are not set, the repo falls back to placeholder `https://YOUR_PRODUCTION_HOST` in `appsettings.Production.json` — refresh cookies and email links will not work until you override them.

Refresh cookies use `Secure=true` and `SameSite=Strict` outside Development ([`RefreshCookieService`](../src/backend/Shora.Infrastructure/Services/RefreshCookieService.cs)).

### Custom domain

When you bind a custom domain (Railway **Settings → Networking** or legacy App Service **Custom domains**):

1. **Portal** → App Service → **Custom domains** → add hostname → complete DNS (CNAME to `<app>.azurewebsites.net` or apex A/ALIAS records).
2. **TLS** → bind **App Service Managed Certificate** (Basic+ plan) or upload your own cert.
3. Update **all** URL-dependent settings to the new HTTPS origin (no trailing slash):

- `Frontend__BaseUrl`
- `Cors__AllowedOrigins__0`
- Google Cloud **Authorized JavaScript origins** (if using Google sign-in)
- `googleClientId` build in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) (same client ID; origins must include the new domain)

## SMTP (required for sending mail)

`Email__Host` and `Email__FromAddress` are **required at startup** in Production. Without a working SMTP setup (including `Email__Password` when using SendGrid), signup may succeed but **verification and transactional emails fail** (spec 02).

| Setting | Variable name | Notes |
| --- | --- | --- |
| SMTP host | `Email__Host` | e.g. `smtp.sendgrid.net` |
| SMTP port | `Email__Port` | Default `587` (StartTLS); use `465` for SSL-on-connect |
| SMTP user | `Email__Username` | SendGrid: literal `apikey` (constant, not a secret) |
| SMTP password | `Email__Password` | SendGrid: API key (`SG.xxx…`) — **secret** |
| From address | `Email__FromAddress` | Verified sender at your provider |
| From display name | `Email__FromName` | Optional; defaults to `Shora` in `appsettings.Production.json` |

Example providers: SendGrid SMTP, Amazon SES, Mailgun, or your domain host’s SMTP. Use the provider’s SMTP hostname, port, and app-specific password — not your login password when the provider offers an API/app password.

## Google OAuth (optional)

Only needed if you want the Google sign-in button on the login page.

| Layer | What to set |
| --- | --- |
| **Backend** | `Google__ClientId` — must match the OAuth client used by the frontend |
| **Frontend (build-time)** | `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) — commit before merge to `main` |
| **Google Cloud Console** | See [Google Cloud setup](#google-cloud-setup) below |

`Google__ClientSecret` is **not used** by the current ID-token flow — only `Google__ClientId` is validated server-side.

### Google Cloud setup

1. [Google Cloud Console](https://console.cloud.google.com/) → **APIs & Services → Credentials** → **Create credentials → OAuth client ID**.
2. Application type: **Web application**.
3. **Authorized JavaScript origins:** add your production HTTPS URL (same as `Frontend__BaseUrl`), e.g. `https://shora-production.up.railway.app`.
4. **Authorized redirect URIs:** not required for the current Google Identity Services button flow (ID token only).
5. Copy the **Client ID** into:
   - Railway `Google__ClientId` (or App Service on legacy path)
   - `googleClientId` in `environment.production.ts`
6. Redeploy (merge to `main`) after changing the frontend file.

First-time Google users sign in from the **login** page (not signup) — spec 02.

## Admin bootstrap (first production admin)

There is no public admin registration. Choose one approach:

### Recommended: one-time `AdminSeed` (then remove)

1. Before first deploy (or before first app restart after deploy), set on Railway (or App Service on legacy path):
   - `AdminSeed__Email` — admin login email
   - `AdminSeed__Password` — strong password (Identity rules apply)
2. On startup, [`DatabaseSeeder`](../src/backend/Shora.Infrastructure/Data/DatabaseSeeder.cs) creates the Admin user if it does not exist.
3. Log in at `/auth/login`, confirm admin dashboard access.
4. **Remove** `AdminSeed__Email` and `AdminSeed__Password` from host variables so credentials are not stored in configuration.

If `AdminSeed` is omitted, the app starts but **no admin exists** — receipt approval and admin settings are unavailable until you add an admin (only practical path today is temporary `AdminSeed`).

## Payment and contact defaults (first run)

On first startup, the singleton `Settings` row is seeded from `Seed:*` in [`appsettings.json`](../src/backend/Shora.Api/appsettings.json) if no row exists yet. Defaults are **placeholder test values**:

| Default | Value |
| --- | --- |
| `ConsultantWhatsAppNumber` | `+201000000000` |
| `VodafoneCashNumber` | `01000000000` |
| `InstaPayHandle` | `consultant@instapay` |

Seeding runs **once** — later changes must go through `/admin/settings` (or set `Seed__*` env vars **before the first app startup**):

| Setting | Variable name |
| --- | --- |
| WhatsApp | `Seed__ConsultantWhatsAppNumber` |
| Vodafone Cash | `Seed__VodafoneCashNumber` |
| InstaPay | `Seed__InstaPayHandle` |
| Optional payment note | `Seed__PaymentInstructions` |

**After first deploy:** log in as admin → **Settings** and set real payment numbers before accepting client bookings.

## Optional tuning (inherits from base `appsettings.json`)

No Azure override needed for MVP unless you want to change defaults:

- `BackgroundJobs`, `OpsMonitoring`, `RateLimiting`, `Cache`, `Booking`, `ReceiptUpload`, `Brand`
- `Jwt__Issuer`, `Jwt__Audience`, token lifetimes

## Frontend (build-time, not Railway/App Service)

| File | Purpose |
| --- | --- |
| [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) | `googleClientId` for Google button; `apiBaseUrl: '/api/v1'` is correct for same-site deploy |
| [`angular.json`](../src/frontend/angular.json) | Production build uses `fileReplacements` to swap in `environment.production.ts` |

Set `googleClientId` in `environment.production.ts` **before merge to `main`** so the Deploy workflow bakes it into the SPA bundle.

**SEO static files:** `robots.txt` and `sitemap.xml` are **not** part of the repo. They are optional for search indexing only — not needed for deploy or booking. If added later, place them in [`src/frontend/public/`](../src/frontend/public/) so they copy into `wwwroot` on build.

## Verify

1. All required host variables saved (no secrets in git).
2. Merge to `main` — **Deploy** runs automatically ([`deploy.yml`](../.github/workflows/deploy.yml)).
3. Open `https://<host>/` (SPA) and `GET https://<host>/api/v1/health` (API).
4. Log in as admin; confirm `/admin/settings` has real payment numbers.
5. Test end-to-end: signup → verification email → book → upload receipt → admin approve.
6. Remove `AdminSeed__*` from Railway (or App Service) after admin account exists.

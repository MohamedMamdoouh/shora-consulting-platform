# Production deployment

Shora runs on **Render** (API + Angular app in one container), **Supabase PostgreSQL**, **Azure Blob** (receipts), and **Brevo** (email).

Push to `main` → Render builds [`Dockerfile`](../Dockerfile) and auto-deploys. GitHub Actions ([`ci.yml`](../.github/workflows/ci.yml)) runs tests only.

Set secrets as **Render environment variables** (`Jwt__SigningKey` → `Jwt:SigningKey`). Never commit credentials. Non-secret structure: [`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json).

## Stack

| Component | Provider |
| --------- | -------- |
| Compute | Render (Docker from Git) |
| Database | Supabase PostgreSQL |
| Receipts | Azure Blob (private container) |
| Email | Brevo HTTPS API |
| CI | GitHub Actions |

## 1. Prerequisites

**Supabase** — create a project at [supabase.com/dashboard](https://supabase.com/dashboard). Pick a region close to your Render service. Save the database password (shown once).

Shora uses EF migrations on startup, manual transactions, and `FOR UPDATE` row locks. Use the **session pooler** (port **5432**), not the transaction pooler (port **6543**).

| Connection type | Port | Use for Shora? |
| --------------- | ---- | -------------- |
| Session pooler (Supavisor) | 5432 | **Recommended on Render** — IPv4-friendly |
| Direct | 5432 | OK if Render can reach it (may need IPv4 add-on) |
| Transaction pooler | 6543 | **Avoid** — breaks session-scoped EF transactions |

In Supabase → **Connect** → **Session pooler**, then convert to **key-value Npgsql format** for Render (avoids `=` truncation in URI strings):

```text
Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;Ssl Mode=Require;Trust Server Certificate=true
```

Session pooler username is `postgres.<project-ref>`, not plain `postgres`.

**Azure Blob** — create a storage account and private container (e.g. `receipts`). Get the connection string via Azure Portal or `az storage account show-connection-string`.

Migrations run automatically on first startup.

**Free-tier note:** Supabase projects pause after inactivity; the first connection after pause may take a few seconds. If connection errors persist with the session pooler, try the **Direct** connection string from the Supabase dashboard (or enable the IPv4 add-on).

## 2. Create Render service

Render Dashboard → **New +** → **Web Service** → connect `MohamedMamdoouh/shora-consulting-platform`:

| Setting | Value |
| ------- | ----- |
| Language | Docker |
| Branch | `main` |
| Auto-Deploy | Yes |
| Health Check Path | `/api/v1/health` |
| Dockerfile Path | `Dockerfile` |
| Docker Build Context | `.` |

Push to `main` deploys automatically. No GitHub deploy workflow or container registry.

## 3. Environment variables

Render → **shora** → **Environment**.

### Required

| Variable | Value |
| -------- | ----- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_HTTP_PORTS` | `8080` |
| `ConnectionStrings__DefaultConnection` | Supabase session-pooler key-value string (see §1) |
| `Jwt__SigningKey` | Random string, 32+ chars |
| `Frontend__BaseUrl` | `https://<your-host>.onrender.com` (no trailing slash) |
| `Cors__AllowedOrigins__0` | Same as `Frontend__BaseUrl` |
| `AllowedHosts` | Hostname only, e.g. `<your-host>.onrender.com` |
| `Storage__ConnectionString` | Azure connection string |
| `Storage__ReceiptContainer` | `receipts` |
| `Email__ApiKey` | Brevo API key |
| `Email__FromAddress` | Verified Brevo sender |

### First-time only

| Variable | Purpose |
| -------- | ------- |
| `AdminSeed__Email` / `AdminSeed__Password` | Create first admin — **remove after login** |
| `Seed__ConsultantWhatsAppNumber`, `Seed__VodafoneCashNumber`, `Seed__InstaPayHandle` | Payment defaults before first startup (or set in `/admin/settings` later) |

### Optional

`Email__FromName`, `Jwt__Issuer`, `Jwt__Audience`, and tuning sections from base `appsettings.json` (`BackgroundJobs`, `RateLimiting`, etc.).

**Common mistakes:**

- `Cors__AllowedOrigins__0` left as `http://localhost:4200` causes startup failure. Must match your live HTTPS URL.
- **Wrong pooler mode:** port `6543` causes migration/transaction failures — use session pooler port `5432`.
- **URI on Render:** passwords containing `=` get truncated — use key-value format, not a `postgresql://` URI.
- **Wrong username:** session pooler requires `postgres.<project-ref>`, not `postgres`.

## 4. Verify

1. Render deploy status **Live**
2. `GET https://<your-host>/api/v1/health` → healthy
3. Site loads at `/`
4. Log in at `/auth/login`
5. Remove `AdminSeed__*` env vars after first admin login

## 5. Operations

| Task | How |
| ---- | --- |
| Redeploy | Push to `main`, or Render → **Manual Deploy** |
| Custom domain | Render → Custom Domains, then update `Frontend__BaseUrl`, `Cors__AllowedOrigins__0`, `AllowedHosts` |
| Local Docker test | `docker build -t shora:local .` then run on port 8080 with prod env vars |
| GitHub | CI only — no deploy secrets needed. Remove legacy `RAILWAY_*` / `RENDER_DEPLOY_HOOK_URL` if present |

**Free tier:** Render web services spin down after 15 minutes of inactivity; builds can be slow. Receipt images use Azure Blob (not local disk).

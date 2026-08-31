# 09 — CI/CD Pipeline

Status: **Done** in repo. **Deploy target:** Render (Git + Docker) + Supabase PostgreSQL + Azure Blob (receipts). Operator guide: [docs/deployment.md](../docs/deployment.md).

This spec defines how Shora is built, validated, and deployed. It complements spec 08 #4 (hosting topology) with GitHub Actions workflows. Workflow YAML stays thin; this document is the authoritative design.

### Workflow files

| File                                                      | Role                                           | Trigger           |
| --------------------------------------------------------- | ---------------------------------------------- | ----------------- |
| [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) | Build + test (backend and frontend separately) | Push/PR to `main` |

Production releases: push to `main` → Render builds [`Dockerfile`](../Dockerfile) and auto-deploys. No separate GitHub deploy workflow.

Operator go-live steps: [docs/deployment.md](../docs/deployment.md).

---

## 1. Goals

- **Fast PR feedback** — every change to `main` is buildable and testable before merge.
- **Reproducible builds** — pinned toolchains (.NET 10, Node 22) and lock files (`package-lock.json`, NuGet restore).
- **Safe deploy path** — production releases on **Render** (single container, Docker build from Git, same-site Angular app + API) with **Supabase PostgreSQL** and **Azure Blob** for receipts only; aligned with spec 02 same-site auth (`SameSite=Strict` refresh cookies). Local development uses `Development` / dev tooling only — no separate staging environment.

## 2. Repository & Triggers

- **Repository:** [MohamedMamdoouh/shora-consulting-platform](https://github.com/MohamedMamdoouh/shora-consulting-platform)
- **Platform:** GitHub Actions (`.github/workflows/`)
- **Triggers (CI):**
  - `push` to `main`
  - `pull_request` targeting `main`
- **Path filters:** backend job runs when `src/backend/**` or `.github/workflows/**` changes; frontend job when `src/frontend/**` or workflows change (via `dorny/paths-filter@v3` in the `changes` job). Reduces runner minutes on monorepo edits outside those trees.

---

## 3. CI path detection

**Purpose:** Decide whether backend or frontend CI jobs should run based on changed paths — saves runner minutes on docs-only edits.

**Workflow:** [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — `changes` job.

| Step        | Action / detail                                                               |
| ----------- | ----------------------------------------------------------------------------- |
| Checkout    | `actions/checkout@v4`                                                         |
| Path filter | `dorny/paths-filter@v3` — outputs `backend`, `frontend`, `workflows` booleans |

Backend and frontend jobs run only when their paths (or `.github/workflows/**`) change.

**Verify:** Push a docs-only change under `specs/` — both build jobs should skip.

---

## 4. Backend CI

**Purpose:** Prove every backend change compiles and passes tests before merge.

**Workflow:** [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — `backend` job (needs `changes`, gated on path filter).

| Step       | Command / action                                       |
| ---------- | ------------------------------------------------------ |
| Checkout   | `actions/checkout@v4`                                  |
| Setup .NET | `actions/setup-dotnet@v4` — `dotnet-version: '10.0.x'` |
| Restore    | `dotnet restore` in `src/backend`                      |
| Build      | `dotnet build --no-restore`                            |
| Test       | `dotnet test --no-build --verbosity normal`            |

- **xUnit tests** in `Shora.Tests` (PostgreSQL via Testcontainers — Docker required on the runner).
- **Cache:** NuGet packages via `setup-dotnet` cache.

**Verify locally:**

```powershell
cd src/backend
dotnet build
dotnet test   # requires Docker
```

Stop any running `Shora.Api` process before building — a running API locks output DLLs.

---

## 5. Frontend CI

**Purpose:** Prove the Angular app builds for production and unit tests pass headlessly.

**Workflow:** [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — `frontend` job (needs `changes`, gated on path filter).

| Step       | Command / action                                                                                |
| ---------- | ----------------------------------------------------------------------------------------------- |
| Checkout   | `actions/checkout@v4`                                                                           |
| Setup Node | `actions/setup-node@v4` — `node-version: '22.x'`, npm cache on `src/frontend/package-lock.json` |
| Install    | `npm ci` in `src/frontend`                                                                      |
| Build      | `npm run build` (production config per `angular.json`)                                          |
| Test       | `CI=true npm test` (headless Vitest via `@angular/build:unit-test`)                             |

- **Cache:** npm dependencies keyed on `package-lock.json`.

**Verify locally:**

```powershell
cd src/frontend
npm ci
npm run build
$env:CI = "true"; npm test
```

---

## 6. CI hygiene

**Purpose:** Keep dependencies current, enforce green CI before merge, and surface pipeline health.

| Item                                                                   | Purpose                                                                           | Status                                                                                      |
| ---------------------------------------------------------------------- | --------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| **Dependabot** ([`.github/dependabot.yml`](../.github/dependabot.yml)) | Monthly PRs for NuGet (`src/backend`) and npm (`src/frontend`) dependency updates | **Done**                                                                                    |
| **Branch protection** (GitHub UI)                                      | Require CI workflow green before merging to `main`                                | **Manual setup** — see [workflows README](../.github/workflows/README.md#branch-protection) |
| **CI badge** ([`README.md`](../README.md))                             | Visibility of pipeline health on the default branch                               | **Done**                                                                                    |

**Branch protection setup** (one-time, after first successful CI run on `main`):

1. GitHub → **Settings → Branches → Add rule** for `main`
2. **Require status checks to pass before merging** — select `Backend` and `Frontend`
3. **Require branches to be up to date before merging** (recommended)

---

## 7. Same-site static hosting

**Purpose:** Enable production model where browser hits **one origin** for both the Angular app and API (required for `SameSite=Strict` refresh cookies, spec 02).

**Done.** Non-Development environments serve static files from [`wwwroot/`](../src/backend/Shora.Api/wwwroot/) and fall back to `index.html` for client routes. `/api/**` remains handled by controllers.

Changes in [`Program.cs`](../src/backend/Shora.Api/Program.cs):

- `UseDefaultFiles()` + `UseStaticFiles()` when not `Development`
- `MapFallbackToFile("index.html")` after `MapControllers()` when not `Development`
- Build output in `wwwroot/` is gitignored (`.gitkeep` only); populated during the Docker build on Render
- Frontend uses relative `apiBaseUrl: '/api/v1'` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts)

**Verify locally:**

```powershell
cd src/frontend
npm run build
# Angular 21 output: dist/shora-web/browser/
Copy-Item -Recurse -Force dist/shora-web/browser/* ../backend/Shora.Api/wwwroot/

cd ../backend
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run --project Shora.Api
# Browse / for the site, /api/v1/health for API — same port
```

---

## 8. Production config contract

**Purpose:** Define the exact environment variables the production host must have — no guessing at deploy time.

**Done.** Structure template: [`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json). Operator guide: [`docs/deployment.md`](../docs/deployment.md). Set on **Render**.

Set secrets via environment variables (double-underscore nesting). Never commit values.

| Setting                                   | Notes                                                                                                      |
| ----------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings__DefaultConnection`    | Supabase PostgreSQL (session pooler, port 5432 — see [`docs/deployment.md`](../docs/deployment.md))        |
| `Jwt__SigningKey`                         | Strong random key, min 32 chars (spec 02)                                                                  |
| `Storage__ConnectionString`               | Blob account — private Azure Storage container (spec 05)                                                   |
| `Storage__ReceiptContainer`               | Private container name (`receipts`)                                                                        |
| `Email__*`                                | Brevo settings (spec 02, outbox) — `ApiKey` + `FromAddress` required at startup                            |
| `Frontend__BaseUrl`                       | Production HTTPS URL (e.g. `https://shora.onrender.com`)                                                   |
| `Cors__AllowedOrigins__0`                 | Same production HTTPS URL (same-site + `AllowCredentials`)                                                 |
| `AllowedHosts`                            | Hostname only (e.g. `shora.onrender.com` — no `https://`)                                                  |
| `AdminSeed__Email`, `AdminSeed__Password` | One-time admin bootstrap — remove from Render after first login                                            |
| `Seed__*`                                 | Optional payment/contact defaults before first startup — see [`docs/deployment.md`](../docs/deployment.md) |

Refresh cookies automatically use `Secure=true` and `SameSite=Strict` outside Development ([`RefreshCookieService`](../src/backend/Shora.Infrastructure/Services/RefreshCookieService.cs)).

**Production URL validation (`ValidateOnStart`):** In non-Development environments, [`FrontendOptionsValidator`](../src/backend/Shora.Application/Options/FrontendOptionsValidator.cs) and [`CorsOptionsValidator`](../src/backend/Shora.Application/Options/CorsOptionsValidator.cs) reject startup when:

- `Frontend:BaseUrl` or any `Cors:AllowedOrigins` entry is missing, not a valid absolute HTTPS URL, or contains the placeholder host `YOUR_PRODUCTION_HOST` (see [`ProductionConfigValidation`](../src/backend/Shora.Application/Options/ProductionConfigValidation.cs)).
- `Cors:AllowedOrigins` is empty.

Set `Frontend__BaseUrl` and `Cors__AllowedOrigins__0` on Render to the same production HTTPS origin (no trailing slash). `http://localhost:4200` fails validation in Production even if baked into JSON — Render env vars override JSON and must match the live URL.

**Verify:** App starts with env vars only; CORS accepts the production origin.

---

## 9. Hosting prerequisites

**Purpose:** Create the resources CD will target.

**Done.** Checklist: [`docs/deployment.md`](../docs/deployment.md).

| Resource               | Purpose                                              |
| ---------------------- | ---------------------------------------------------- |
| **Render**             | Host .NET 10 API + static Angular (Docker from Git)  |
| **Supabase PostgreSQL** | Production database                                  |
| **Azure Blob Storage** | Private receipt container (spec 05)                  |

**Verify:** Render env vars configured; push to `main` triggers build; `GET https://<production-url>/api/v1/health` returns OK.

---

## 10. Production Docker image

**Purpose:** Reproducible production image that bundles frontend + API — built by Render from [`Dockerfile`](../Dockerfile).

**Done.** Multi-stage Docker build at repo root:

1. **frontend** — `npm ci` + `npm run build` in `src/frontend` (includes `src/contracts` for `@contracts/*` imports)
2. **backend** — copy Angular output to `Shora.Api/wwwroot`, `dotnet publish`
3. **runtime** — `mcr.microsoft.com/dotnet/aspnet:10.0`, port 8080

**Verify:** `docker build -t shora:local .` locally (requires Docker), or push to `main` and watch Render build logs.

---

## 11. Render auto-deploy

**Purpose:** Deploy production automatically when `main` is updated, after hosting exists.

**Done.** Render web service connected to GitHub with **Auto-Deploy** on `main`. Setup: [docs/deployment.md](../docs/deployment.md), [`.github/workflows/README.md`](../.github/workflows/README.md).

| Concern          | Design                                                                                        |
| ---------------- | --------------------------------------------------------------------------------------------- |
| **Triggers**     | Push to `main` on GitHub → Render Docker build + deploy                                       |
| **Build**        | Multi-stage sequence inside [`Dockerfile`](../Dockerfile)                                     |
| **Deploy target**| Render web service (Git + Docker)                                                             |
| **GitHub**       | CI only ([`ci.yml`](../.github/workflows/ci.yml)) — no deploy secrets required on GitHub      |

### Render setup

1. Supabase + Azure Blob + Render web service — [docs/deployment.md](../docs/deployment.md)
2. Connect repo `MohamedMamdoouh/shora-consulting-platform`, branch `main`, language **Docker**
3. **Dockerfile Path:** `Dockerfile` · **Build context:** `.`
4. Health check: `/api/v1/health`
5. Set production env vars on Render (see §8)
6. Enable branch protection on `main` — CI green before merge, then push auto-deploys on Render

### Deploy sequence

1. CI should pass on the PR before merge (branch protection recommended); merge to `main` triggers Render
2. Render builds the Docker image and deploys
3. Operator verifies manually: `GET /api/v1/health`, `/`, and `/about` on production URL
4. App startup applies EF migrations and idempotent seed (see §12)

**Verify:** After Render is configured, merge to `main` deploys automatically; app reachable over HTTPS with same-site cookies.

---

## 12. Startup migrations & rollback policy

**Purpose:** Clarify operational behavior for schema changes at deploy time.

**Done in code** — [`Program.cs`](../src/backend/Shora.Api/Program.cs) calls `InitializeDatabaseAsync()` → `MigrateAsync` + idempotent seed ([`DependencyInjection.cs`](../src/backend/Shora.Infrastructure/DependencyInjection.cs)).

- **CI:** backend tests spin up PostgreSQL via Testcontainers (Docker on `ubuntu-latest`).
- **CD (MVP):** no separate `dotnet ef database update` step in the pipeline — deploy relies on startup migration (spec 01 #5, spec 08 #4).
- **Rollback:** redeploying an older app binary does **not** revert the database schema. Migrations are forward-only. If a bad migration ships, restore from backup and ship a fix migration — not automated in MVP.

---

## Same-site deploy model (cross-cutting)

MVP requires frontend and API on the **same registrable domain** over HTTPS (spec 02 #deployment constraint, spec 08 #4).

- Angular static files served from the API host (`wwwroot`) — see §7.
- API routes remain under `/api/**`.
- **Not** split across unrelated subdomains with cross-site cookies in MVP.
- CORS configured for the single app origin with `AllowCredentials` (spec 08 #4).

---

## Out of Scope (MVP Pipeline)

- Multi-region or blue/green deploy
- Full APM / deployment smoke-test suite (add incrementally post-MVP)
- Deploy gated on CI workflow completion (CI runs on PR; Render deploys on push to `main` — use branch protection so only green PRs merge)

---

## Local parity

Run the same commands CI runs before pushing:

```powershell
# Backend
cd src/backend
dotnet build
dotnet test

# Frontend
cd ../frontend
npm ci
npm run build
CI=true npm test
```

**Production Docker image:** `docker build -t shora:local .` from repo root, or push to `main` and watch Render build logs. See [`Dockerfile`](../Dockerfile) and [docs/deployment.md](../docs/deployment.md).

**Note:** stop any running `Shora.Api` process before `dotnet build` locally — a running API locks output DLLs and breaks the build.

Operational summary for workflows: [`.github/workflows/README.md`](../.github/workflows/README.md).

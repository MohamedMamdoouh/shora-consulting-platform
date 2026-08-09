# 09 — CI/CD Pipeline

Status: **Sub-phases 09.1–09.10 done**. Provision Azure in Portal ([docs/azure-prerequisites.md](../docs/azure-prerequisites.md)), then configure GitHub secrets for first production deploy.

This spec defines how Shora is built, validated, and deployed. It complements spec 08 #4 (hosting topology) with GitHub Actions workflows and sub-phases 09.1–09.10. Workflow YAML stays thin; this document is the authoritative design.

### Workflow files

| File | Role | Trigger |
| --- | --- | --- |
| [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) | Build + test (backend and frontend separately) | Push/PR to `main` |
| [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) | Production publish artifact + Azure upload | Push to `main` only |

Operator go-live steps: [docs/README.md](../docs/README.md).

### Implementation summary

| Sub-phase | Scope | Status |
| --- | --- | --- |
| 09.1 | CI path detection (`changes` job) | **Done** |
| 09.2 | Backend CI (restore, build, test) | **Done** |
| 09.3 | Frontend CI (install, build, test) | **Done** |
| 09.4 | CI hygiene (Dependabot, branch protection, CI badge) | **Done** |
| 09.5 | Same-site static hosting (`wwwroot`, SPA fallback) | **Done** |
| 09.6 | Production config contract (env vars, CORS origin) | **Done** |
| 09.7 | Azure prerequisites (App Service, SQL, Blob) | **Done** (Portal checklist) |
| 09.8 | Publish artifact build (npm → wwwroot → dotnet publish) | **Done** (deploy.yml) |
| 09.9 | Deploy workflow (`deploy.yml`, production gate) | **Done** (needs Azure + GitHub secrets to run) |
| 09.10 | Startup migrations & rollback policy | **Done** (code) |

---

## 1. Goals

- **Fast PR feedback** — every change to `main` is buildable and testable before merge.
- **Reproducible builds** — pinned toolchains (.NET 10, Node 22) and lock files (`package-lock.json`, NuGet restore).
- **Safe deploy path (09.5–09.9)** — production releases aligned with spec 08 hosting (Azure App Service + Azure SQL + Blob) and spec 02 same-site auth (`SameSite=Strict` refresh cookies). Local development uses `Development` / dev tooling only — no separate staging environment.

## 2. Repository & Triggers

- **Repository:** [MohamedMamdoouh/shora-consulting-platform](https://github.com/MohamedMamdoouh/shora-consulting-platform)
- **Platform:** GitHub Actions (`.github/workflows/`)
- **Triggers (CI):**
  - `push` to `main`
  - `pull_request` targeting `main`
- **Path filters:** backend job runs when `src/backend/**` or `.github/workflows/**` changes; frontend job when `src/frontend/**` or workflows change (via `dorny/paths-filter@v3` in the `changes` job). Reduces runner minutes on monorepo edits outside those trees.

---

## 09.1 — CI path detection

**Purpose:** Decide whether backend or frontend CI jobs should run based on changed paths — saves runner minutes on docs-only edits.

**Workflow:** [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — `changes` job.

| Step | Action / detail |
| --- | --- |
| Checkout | `actions/checkout@v4` |
| Path filter | `dorny/paths-filter@v3` — outputs `backend`, `frontend`, `workflows` booleans |

Backend and frontend jobs run only when their paths (or `.github/workflows/**`) change.

**Verify:** Push a docs-only change under `specs/` — both build jobs should skip.

---

## 09.2 — Backend CI

**Purpose:** Prove every backend change compiles and passes ~240 tests before merge.

**Workflow:** [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — `backend` job (needs `changes`, gated on path filter).

| Step | Command / action |
| --- | --- |
| Checkout | `actions/checkout@v4` |
| Setup .NET | `actions/setup-dotnet@v4` — `dotnet-version: '10.0.x'` |
| Restore | `dotnet restore` in `src/backend` |
| Build | `dotnet build --no-restore` |
| Test | `dotnet test --no-build --verbosity normal` |

- **~240 xUnit tests** in `Shora.Tests` (59 test classes; SQL Server via Testcontainers — Docker required on the runner).
- **Cache:** NuGet packages via `setup-dotnet` cache.

**Verify locally:**

```powershell
cd src/backend
dotnet build
dotnet test   # requires Docker
```

Stop any running `Shora.Api` process before building — a running API locks output DLLs.

---

## 09.3 — Frontend CI

**Purpose:** Prove the Angular app builds for production and unit tests pass headlessly.

**Workflow:** [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — `frontend` job (needs `changes`, gated on path filter).

| Step | Command / action |
| --- | --- |
| Checkout | `actions/checkout@v4` |
| Setup Node | `actions/setup-node@v4` — `node-version: '22.x'`, npm cache on `src/frontend/package-lock.json` |
| Install | `npm ci` in `src/frontend` |
| Build | `npm run build` (production config per `angular.json`) |
| Test | `CI=true npm test` (headless Vitest via `@angular/build:unit-test`) |

- **Cache:** npm dependencies keyed on `package-lock.json`.

**Verify locally:**

```powershell
cd src/frontend
npm ci
npm run build
$env:CI = "true"; npm test
```

---

## 09.4 — CI hygiene

**Purpose:** Keep dependencies current, enforce green CI before merge, and surface pipeline health.

| Item | Purpose | Status |
| --- | --- | --- |
| **Dependabot** ([`.github/dependabot.yml`](../.github/dependabot.yml)) | Monthly PRs for NuGet (`src/backend`) and npm (`src/frontend`) dependency updates | **Done** |
| **Branch protection** (GitHub UI) | Require CI workflow green before merging to `main` | **Manual setup** — see [workflows README](../.github/workflows/README.md#branch-protection-094) |
| **CI badge** ([`README.md`](../README.md)) | Visibility of pipeline health on the default branch | **Done** |

**Branch protection setup** (one-time, after first successful CI run on `main`):

1. GitHub → **Settings → Branches → Add rule** for `main`
2. **Require status checks to pass before merging** — select `Backend` and `Frontend`
3. **Require branches to be up to date before merging** (recommended)

---

## 09.5 — Same-site static hosting

**Purpose:** Enable production model where browser hits **one origin** for both SPA and API (required for `SameSite=Strict` refresh cookies, spec 02).

**Done.** Non-Development environments serve static files from [`wwwroot/`](../src/backend/Shora.Api/wwwroot/) and fall back to `index.html` for client routes. `/api/**` remains handled by controllers.

Changes in [`Program.cs`](../src/backend/Shora.Api/Program.cs):

- `UseDefaultFiles()` + `UseStaticFiles()` when not `Development`
- `MapFallbackToFile("index.html")` after `MapControllers()` when not `Development`
- Build output in `wwwroot/` is gitignored (`.gitkeep` only); populated at publish time (09.8)
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
# Browse / for SPA, /api/v1/health for API — same port
```

---

## 09.6 — Production config contract

**Purpose:** Define the exact environment variables Azure App Service must have — no guessing at deploy time.

**Done.** Structure template: [`appsettings.Production.json`](../src/backend/Shora.Api/appsettings.Production.json). Operator guide: [`docs/production-config.md`](../docs/production-config.md).

Set secrets via environment variables (double-underscore nesting). Never commit values.

| Setting | Notes |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Azure SQL connection string |
| `Jwt__SigningKey` | Strong random key, min 32 chars (spec 02) |
| `Storage__ConnectionString` | Blob account (spec 05) |
| `Storage__ReceiptContainer` | Private container name |
| `Google__ClientId` | Google OAuth client ID (optional; spec 02) — `ClientSecret` unused by ID-token flow |
| `Email__*` | SMTP / provider settings (spec 02, outbox) |
| `Frontend__BaseUrl` | Production HTTPS URL (transaction email links) |
| `Cors__AllowedOrigins__0` | Same production HTTPS URL (same-site + `AllowCredentials`) |
| `AdminSeed__Email`, `AdminSeed__Password` | One-time admin bootstrap — remove from App Service after first login |
| `Seed__*` | Optional payment/contact defaults before first startup — see [`docs/production-config.md`](../docs/production-config.md) |

Refresh cookies automatically use `Secure=true` and `SameSite=Strict` outside Development ([`RefreshCookieService`](../src/backend/Shora.Infrastructure/Services/RefreshCookieService.cs)).

**Verify:** App starts with env vars only; CORS accepts the production origin.

---

## 09.7 — Azure prerequisites

**Purpose:** Create the hosting resources CD will target.

**Done.** Portal checklist: [`docs/azure-prerequisites.md`](../docs/azure-prerequisites.md).

| Resource | Purpose |
| --- | --- |
| **Azure App Service** | Host .NET 10 API + static Angular (always-on, single instance) |
| **Azure SQL** | Production database |
| **Azure Blob Storage** | Private receipt container (`Storage:ReceiptContainer`, spec 05) |

**Verify:** App settings configured in Portal; merge to `main` deploys automatically; `/api/v1/health` returns OK.

---

## 09.8 — Publish artifact build

**Purpose:** Reproducible release folder that bundles frontend + API — the artifact `deploy.yml` uploads.

**Done.** Implemented in [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) `build` job (no local script — deploy via GitHub Actions only).

Sequence:

1. `npm ci` + `npm run build` in `src/frontend`
2. Copy `dist/shora-web/browser/*` → `src/backend/Shora.Api/wwwroot/`
3. `dotnet publish Shora.Api -c Release -o ./publish`

**Verify:** Run the **Deploy** workflow from GitHub Actions after Azure infra (09.7) and secrets (09.9) are configured.

---

## 09.9 — Deploy workflow

**Purpose:** Automate pushing the publish artifact to Azure after 09.5–09.8 are proven and Azure exists (09.7).

**Done.** Workflow: [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml). Setup: [docs/azure-prerequisites.md](../docs/azure-prerequisites.md), [`.github/workflows/README.md`](../.github/workflows/README.md).

| Concern | Design |
| --- | --- |
| **Triggers** | Push to `main` only — no manual dispatch |
| **Environments** | GitHub Environment `production` (optional approval gate) |
| **Missing Azure config** | Deploy job **Require Azure configuration** step fails if `AZURE_WEBAPP_NAME` is unset — no silent skip |
| **Build** | Same sequence as 09.8 in the `build` job |
| **Deploy target** | Azure App Service (Linux, .NET 10, always-on, **1 instance**) |
| **Auth** | Publish profile secret (`AZURE_WEBAPP_PUBLISH_PROFILE`); OIDC optional later |

### GitHub setup

1. Azure resources + app settings — [docs/azure-prerequisites.md](../docs/azure-prerequisites.md)
2. GitHub Environment `production` (optional reviewers)
3. Repository variable `AZURE_WEBAPP_NAME` (deploy fails with error if missing)
4. Optional repository variable `DEPLOY_ENVIRONMENT` (defaults to `production`)
5. Environment secret `AZURE_WEBAPP_PUBLISH_PROFILE` on that environment
6. Enable branch protection on `main` — CI green before merge, then push auto-deploys

### Deploy sequence

1. CI should pass on the PR before merge (branch protection recommended); merge to `main` triggers Deploy
2. Build job produces publish artifact (09.8)
3. Deploy job uploads to App Service
4. App startup applies EF migrations and idempotent seed (09.10)

**Verify:** After Azure + GitHub secrets are configured, merge to `main` deploys automatically; app reachable over HTTPS with same-site cookies.

---

## 09.10 — Startup migrations & rollback policy

**Purpose:** Clarify operational behavior for schema changes at deploy time.

**Done in code** — [`Program.cs`](../src/backend/Shora.Api/Program.cs) calls `InitializeDatabaseAsync()` → `MigrateAsync` + idempotent seed ([`DependencyInjection.cs`](../src/backend/Shora.Infrastructure/DependencyInjection.cs)).

- **CI:** backend tests spin up SQL Server via Testcontainers (Docker on `ubuntu-latest`).
- **CD (MVP):** no separate `dotnet ef database update` step in the pipeline — deploy relies on startup migration (spec 01 #5, spec 08 #4).
- **Rollback:** redeploying an older app binary does **not** revert the database schema. Migrations are forward-only. If a bad migration ships, restore from backup and ship a fix migration — not automated in MVP.

---

## Same-site deploy model (cross-cutting)

MVP requires frontend and API on the **same registrable domain** over HTTPS (spec 02 #deployment constraint, spec 08 #4).

- Angular static files served from the API host (`wwwroot`) — see 09.5.
- API routes remain under `/api/**`.
- **Not** split across unrelated subdomains with cross-site cookies in MVP.
- CORS configured for the single app origin with `AllowCredentials` (spec 08 #4).

---

## Out of Scope (MVP Pipeline)

- Docker image build/push and container registry deploy (unless hosting choice changes)
- Multi-region or blue/green deploy
- Full APM / deployment smoke-test suite (add incrementally post-MVP)
- Deploy gated on CI workflow completion (CI runs on PR; Deploy runs on push to `main` — use branch protection so only green PRs merge)

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

**Full publish artifact (09.8):** use the **Deploy** workflow in GitHub Actions — see [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml).

**Note:** stop any running `Shora.Api` process before `dotnet build` locally — a running API locks output DLLs and breaks the build.

Operational summary for workflows: [`.github/workflows/README.md`](../.github/workflows/README.md).

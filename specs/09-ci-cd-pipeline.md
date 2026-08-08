# 09 — CI/CD Pipeline

Status: **Phase 1 CI implemented** (`.github/workflows/ci.yml` — backend + frontend build/test on push/PR to `main`). **Phase 2 CD** (Azure App Service deploy) is planned, not yet implemented.

This spec defines how Shora is built, validated, and (later) deployed. It complements spec 08 #4 (hosting topology) with concrete GitHub Actions workflows and a phased rollout plan. Workflow YAML stays thin; this document is the authoritative design.

## 1. Goals

- **Fast PR feedback** — every change to `main` is buildable and testable before merge.
- **Reproducible builds** — pinned toolchains (.NET 10, Node 22) and lock files (`package-lock.json`, NuGet restore).
- **Safe deploy path (Phase 2)** — staging and production releases aligned with spec 08 hosting (Azure App Service + Azure SQL + Blob) and spec 02 same-site auth (`SameSite=Strict` refresh cookies).

## 2. Repository & Triggers

- **Repository:** [MohamedMamdoouh/shora-consulting-platform](https://github.com/MohamedMamdoouh/shora-consulting-platform)
- **Platform:** GitHub Actions (`.github/workflows/`)
- **Triggers (Phase 1 CI):**
  - `push` to `main`
  - `pull_request` targeting `main`
- **Path filters:** backend job runs when `src/backend/**` or `.github/workflows/**` changes; frontend job when `src/frontend/**` or workflows change (via `dorny/paths-filter@v3` in the `changes` job). Reduces runner minutes on monorepo edits outside those trees.

## 3. Phase 1 — CI

Workflow file: `[.github/workflows/ci.yml](../.github/workflows/ci.yml)`

Three jobs — a **change-detection** gate plus two **parallel** build/test jobs. No secrets required; read-only `contents` permission.

### 3.0 Change detection job

| Step        | Action / detail                                                                 |
| ----------- | ------------------------------------------------------------------------------- |
| Checkout    | `actions/checkout@v4`                                                           |
| Path filter | `dorny/paths-filter@v3` — outputs `backend`, `frontend`, `workflows` booleans |

Backend and frontend jobs run only when their paths (or `.github/workflows/**`) change. Docs/specs-only edits skip the affected job(s).

### 3.1 Backend job

| Step       | Command / action                                       |
| ---------- | ------------------------------------------------------ |
| Checkout   | `actions/checkout@v4`                                  |
| Setup .NET | `actions/setup-dotnet@v4` — `dotnet-version: '10.0.x'` |
| Restore    | `dotnet restore` in `src/backend`                      |
| Build      | `dotnet build --no-restore`                            |
| Test       | `dotnet test --no-build --verbosity normal`            |

- **~240 xUnit tests** in `Shora.Tests` (59 test classes; SQL Server via Testcontainers — Docker required on the runner).
- **Cache:** NuGet packages via `setup-dotnet` cache.

### 3.2 Frontend job

| Step       | Command / action                                                                                |
| ---------- | ----------------------------------------------------------------------------------------------- |
| Checkout   | `actions/checkout@v4`                                                                           |
| Setup Node | `actions/setup-node@v4` — `node-version: '22.x'`, npm cache on `src/frontend/package-lock.json` |
| Install    | `npm ci` in `src/frontend`                                                                      |
| Build      | `npm run build` (production config per `angular.json`)                                          |
| Test       | `CI=true npm test` (headless Vitest via `@angular/build:unit-test`)                             |

- **Cache:** npm dependencies keyed on `package-lock.json`.

## 4. Phase 1 — Supporting Automation

| Item                                      | Purpose                                                                                |
| ----------------------------------------- | -------------------------------------------------------------------------------------- |
| **Dependabot** (`.github/dependabot.yml`) | Weekly PRs for NuGet (`src/backend`) and npm (`src/frontend`) dependency updates       |
| **Branch protection** (GitHub UI)         | Require CI workflow green before merging to `main` — enable after first successful run |
| **CI badge** (README)                     | Visibility of pipeline health on the default branch                                    |

## 5. Phase 2 — CD (future)

**Not implemented until Azure resources exist.** Core MVP features (specs 02–07) are implemented in code; CD awaits Azure provisioning and secrets setup.

Planned workflow: `.github/workflows/deploy.yml` (separate from CI).

| Concern           | Design                                                                                                                                                                                                       |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Environments**  | GitHub Environments: `staging`, `production` (production requires manual approval / reviewers)                                                                                                               |
| **Triggers**      | `workflow_dispatch` for staging; tag `v`\* (semver) for production — not auto-deploy on every merge to `main`                                                                                                |
| **Build**         | `npm run build` → copy Angular output into API `wwwroot` → `dotnet publish`                                                                                                                                  |
| **Deploy target** | Azure App Service (Linux, .NET 10, always-on, **instance count = 1**, no autoscaling rule — background jobs run in-process, spec 08 #3). No load balancer or traffic manager in front of multiple instances. |
| **Secrets**       | App Service application settings or Azure Key Vault references — never in repo                                                                                                                               |

### Deploy sequence (planned)

1. CI workflow passes on the commit/tag to deploy.
2. Build frontend production bundle.
3. Publish API with static files embedded (same-site model).
4. Deploy artifact to App Service (e.g. `azure/webapps-deploy` or OIDC federated login).
5. App startup applies EF migrations and idempotent seed (spec 09 #8) — no separate migration job for MVP.

## 6. Azure Prerequisites (CD)

Cross-ref [spec 08 #4](08-cross-cutting-concerns.md). Required before Phase 2:

| Resource               | Purpose                                                                                                         |
| ---------------------- | --------------------------------------------------------------------------------------------------------------- |
| **Azure App Service**  | Host .NET 10 API + static Angular (always-on, single instance)                                                  |
| **Azure SQL**          | Production database                                                                                             |
| **Azure Blob Storage** | Private receipt container (`Storage:ReceiptContainer`, spec 05) — **implemented**; provision account for deploy |

### Application settings / secrets (production)

Set via environment variables (double-underscore nesting). Never commit values.

| Setting                                    | Notes                                                                 |
| ------------------------------------------ | --------------------------------------------------------------------- |
| `ConnectionStrings__DefaultConnection`     | Azure SQL connection string                                           |
| `Jwt__SigningKey`                          | Strong random key (spec 02)                                           |
| `Storage__ConnectionString`                | Blob account (spec 05)                                                |
| `Storage__ReceiptContainer`                | Private container name                                                |
| `Google__ClientId`, `Google__ClientSecret` | Google OAuth (spec 02)                                                |
| `Email__*`                                 | SMTP / provider settings (spec 02, outbox)                            |
| `AdminSeed__Email`, `AdminSeed__Password`  | One-time admin bootstrap (optional; prefer manual seed in production) |

GitHub Actions CD will use **OIDC federated credentials** to Azure (preferred over long-lived service principal secrets) or repository/environment secrets for the publish profile — decision at Phase 2 implementation time.

## 7. Same-Site Deploy Model

MVP requires frontend and API on the **same registrable domain** over HTTPS (spec 02 #deployment constraint, spec 08 #4).

- Angular static files served from the API host (e.g. `wwwroot`) or reverse-proxied under the same origin.
- API routes remain under `/api/**`.
- **Not** split across unrelated subdomains with cross-site cookies in MVP.
- CORS configured for the single app origin with `AllowCredentials` (spec 08 #4).

## 8. Migrations & Startup

EF Core migrations and idempotent seed run on application startup (`[Program.cs](../src/backend/Shora.Api/Program.cs)` — `InitializeDatabaseAsync`).

- **CI:** backend tests spin up SQL Server via Testcontainers (Docker on `ubuntu-latest`).
- **CD (MVP):** no separate `dotnet ef database update` step in the pipeline — deploy relies on startup migration (spec 01 #5, spec 08 #4).
- **Rollback:** application rollback does not auto-revert schema; forward-only migrations require operational runbook if a bad migration ships (out of MVP automation scope).

## 9. Out of Scope (MVP Pipeline)

- Docker image build/push and container registry deploy (unless hosting choice changes)
- Multi-region or blue/green deploy
- Automated production deploy on every merge to `main`
- Full APM / deployment smoke-test suite (add incrementally post-MVP)

## 10. Local Parity

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

**Note:** stop any running `Shora.Api` process before `dotnet build` locally — a running API locks output DLLs and breaks the build.

Operational summary for workflows: `[.github/workflows/README.md](../.github/workflows/README.md)`.

# Operator documentation

Pointers for running Shora in production. Application code lives under `src/`; these docs cover hosting and configuration only.

| Doc | When to use |
| --- | --- |
| [railway-prerequisites.md](railway-prerequisites.md) | **Active path** — Railway + Neon PostgreSQL + Azure Storage (receipts), GitHub secrets (Path B-lite) |
| [azure-prerequisites.md](azure-prerequisites.md) | **Legacy** — App Service deploy when Azure quota is available (Path A) |
| [production-config.md](production-config.md) | **App settings** — connection strings, JWT, SMTP, OAuth, admin bootstrap (spec 09.6) |
| [ops-runbooks.md](ops-runbooks.md) | **Incidents** — ops alert runbooks (spec 08.9) |

## Go-live order (Path B-lite — Railway)

1. Neon PostgreSQL + Azure Blob storage (receipts) — [railway-prerequisites.md § B1](railway-prerequisites.md#b1--neon--azure-storage).
2. Railway project, domain, variables — [railway-prerequisites.md § B2–B4](railway-prerequisites.md#b2--railway-project).
3. Google OAuth (optional) — origin = Railway HTTPS URL; `googleClientId` in `environment.production.ts`.
4. GitHub: `RAILWAY_SERVICE_ID`, `PRODUCTION_URL`, secret `RAILWAY_TOKEN` — [railway-prerequisites.md § B5](railway-prerequisites.md#b5--github).
5. Merge to `main` → Deploy workflow → smoke tests.
6. Admin login, E2E, remove `AdminSeed__*`.

See [azure-prerequisites.md](azure-prerequisites.md) for the legacy App Service path.

## CI vs deploy (what runs when)

| Workflow | File | Trigger | Purpose |
| --- | --- | --- | --- |
| **CI** | [ci.yml](../.github/workflows/ci.yml) | Every push/PR to `main` | Build + test backend and frontend separately |
| **Deploy** | [deploy.yml](../.github/workflows/deploy.yml) | Push to `main` | Build artifact → GHCR → Railway redeploy |

CI validates on PRs; **Deploy runs automatically when changes land on `main`** (after Railway + Azure + GitHub secrets are configured). Enable branch protection so only CI-green PRs merge.

Design detail: [spec 09](../specs/09-ci-cd-pipeline.md).

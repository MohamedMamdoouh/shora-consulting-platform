# Operator documentation

Pointers for running Shora in production. Application code lives under `src/`; these docs cover hosting and configuration only.

| Doc | When to use |
| --- | --- |
| [railway-prerequisites.md](railway-prerequisites.md) | **Active path** — Railway + Neon PostgreSQL + Azure Storage (receipts), GitHub secrets (Path B-lite) |
| [production-config.md](production-config.md) | **App settings** — connection strings, JWT, SMTP, OAuth, admin bootstrap (spec 09.6) |
| [azure-prerequisites.md](azure-prerequisites.md) | **Legacy** — App Service + Azure SQL when Azure quota is available (Path A) |
| [ops-runbooks.md](ops-runbooks.md) | **Incidents** — ops alert runbooks (spec 08.9) |

## Production status (2026-08-12)

**Repo:** [MohamedMamdoouh/shora-consulting-platform](https://github.com/MohamedMamdoouh/shora-consulting-platform) · **Branch:** `main` (PostgreSQL + Railway deploy path)

| Step | Item | Status |
| --- | --- | --- |
| — | **CI** (backend + frontend tests on PostgreSQL Testcontainers) | **Done** |
| B1 | Neon PostgreSQL (`shora-prod`) | **Done** |
| B1 | Azure Blob (`stshoraprodne001` / `receipts`) | **Done** |
| B2 | Railway service + public domain | **Done** |
| B4 | Railway variables | **Done** (operator) |
| B5 | GitHub `RAILWAY_*` + `PRODUCTION_URL` | **Operator** — `RAILWAY_TOKEN` = **project token** from Railway project **shora** (environment **secret** on GitHub Environment `production`, not a repo secret; not an account token) |
| B3 | GHCR image pull (public package or registry auth) | **Pending** |
| — | Deploy workflow: build → push GHCR (lowercase tag) → `railway redeploy` | **Code done** — blocked until B3/B5 complete |
| — | First successful Railway deploy + `/api/v1/health` | **Pending** |
| — | Admin login, payment settings, remove `AdminSeed__*` | **Pending** |

### Provisioned resources (non-secret)

| Resource | Value |
| --- | --- |
| **Production URL** | `https://shora-production.up.railway.app` |
| **Railway project** | `shora` |
| **Railway project ID** | `2a876d36-cf24-4cf2-b6bf-ed0d1eee6e07` |
| **Railway environment ID** | `907c0f6e-5118-459c-a7ea-d273663664d1` (`production`) |
| **Railway service** | `Shora` |
| **Railway service ID** | `f69a711c-b830-4a97-a269-fa5e2b6f4dc9` |
| **Docker image** | `ghcr.io/mohamedmamdoouh/shora-consulting-platform:production` |
| **Health check** | `/api/v1/health` |
| **Neon project** | `shora-prod` |
| **Azure resource group** | `rg-shora-prod-ne` (North Europe) |
| **Azure storage account** | `stshoraprodne001` |
| **Blob container** | `receipts` (private) |

Secrets (Neon connection string, JWT, Azure storage key, SendGrid API key, `AdminSeed__*`) live in **Railway variables only** — never in git.

### What to do next

1. Finish [GitHub setup](railway-prerequisites.md#b5--github) — especially `RAILWAY_TOKEN` (project token from **shora** → Tokens → `production`) on the **`production`** environment.
2. Confirm Railway variables `Frontend__BaseUrl` and `Cors__AllowedOrigins__0` are `https://shora-production.up.railway.app` (not `http://localhost:4200`).
3. [Make the GHCR package public](railway-prerequisites.md#b3--ghcr-image-pull) (or add Railway registry credentials).
4. Merge to `main` or re-run **Deploy** — image tag is `ghcr.io/mohamedmamdoouh/shora-consulting-platform:production` (lowercase; required by GHCR).
5. [Redeploy on Railway](railway-prerequisites.md#manual-redeploy) if the workflow succeeded but the service did not pick up the image.
6. Verify `GET https://shora-production.up.railway.app/api/v1/health` → `healthy`.
7. Complete [post-deploy checklist](railway-prerequisites.md#verify).

---

## Go-live order (Path B-lite — Railway)

1. Neon PostgreSQL + Azure Blob storage (receipts) — [railway-prerequisites.md § B1](railway-prerequisites.md#b1--neon--azure-storage).
2. Railway project, service, domain — [railway-prerequisites.md § B2](railway-prerequisites.md#b2--railway-project).
3. Railway variables — [railway-prerequisites.md § B4](railway-prerequisites.md#b4--railway-variables).
4. GHCR pull access — [railway-prerequisites.md § B3](railway-prerequisites.md#b3--ghcr-image-pull).
5. GitHub: `RAILWAY_SERVICE_ID`, `PRODUCTION_URL`, secret `RAILWAY_TOKEN` — [railway-prerequisites.md § B5](railway-prerequisites.md#b5--github).
6. Merge to `main` (or re-run **Deploy**) → smoke tests.
7. Admin login, update `/admin/settings`, E2E test, remove `AdminSeed__*`.

Optional: Google OAuth — origin = Railway HTTPS URL; `googleClientId` in `environment.production.ts`.

See [azure-prerequisites.md](azure-prerequisites.md) for the legacy App Service path.

## CI vs deploy (what runs when)

| Workflow | File | Trigger | Purpose |
| --- | --- | --- | --- |
| **CI** | [ci.yml](../.github/workflows/ci.yml) | Every push/PR to `main` | Build + test backend and frontend separately |
| **Deploy** | [deploy.yml](../.github/workflows/deploy.yml) | Push to `main` | Build artifact → GHCR → Railway redeploy |

CI validates on PRs; **Deploy runs automatically when changes land on `main`** (after Railway + Neon/Azure + GitHub secrets are configured). Enable branch protection so only CI-green PRs merge.

Design detail: [spec 09](../specs/09-ci-cd-pipeline.md).

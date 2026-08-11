# GitHub Actions workflows

Full CI/CD design: [specs/09-ci-cd-pipeline.md](../specs/09-ci-cd-pipeline.md) · Production status: [docs/README.md](../docs/README.md)

## Shora production (current)

| Item | Value |
| --- | --- |
| URL | `https://shora-production.up.railway.app` |
| Railway project | `shora` |
| Railway project ID | `2a876d36-cf24-4cf2-b6bf-ed0d1eee6e07` |
| Railway environment ID | `907c0f6e-5118-459c-a7ea-d273663664d1` (`production`) |
| Railway service ID | `f69a711c-b830-4a97-a269-fa5e2b6f4dc9` |
| GHCR image | `ghcr.io/mohamedmamdoouh/shora-consulting-platform:production` |
| GitHub repo | `MohamedMamdoouh/shora-consulting-platform` |

See [docs/railway-prerequisites.md](../docs/railway-prerequisites.md) for GHCR pull access and remaining verify steps.

## Overview

| Workflow | File | Runs when | Deploys? |
| --- | --- | --- | --- |
| **CI** | [`ci.yml`](ci.yml) | Every push and PR to `main` | No — validates code only |
| **Deploy** | [`deploy.yml`](deploy.yml) | Push to `main` | Yes — builds publish artifact, pushes container to GHCR, redeploys Railway |

**CI** and **Deploy** serve different jobs. CI keeps `main` healthy; Deploy ships a release after Railway, Neon/Azure Storage, and GitHub secrets exist.

---

## CI (`ci.yml`)

| Job | When it runs | Working directory | Steps |
| --- | --- | --- | --- |
| **Changes** | Always | repo root | `dorny/paths-filter@v3` — sets backend/frontend flags |
| **Backend** | `src/backend/**` or `.github/workflows/**` changed | `src/backend` | `dotnet restore` → `build` → `test` |
| **Frontend** | `src/frontend/**` or `.github/workflows/**` changed | `src/frontend` | `npm ci` → `npm run build` → `npm test` (`CI=true`) |

- Backend tests use Testcontainers PostgreSQL (Docker required on the runner).
- Docs/spec-only changes skip the jobs that did not touch backend or frontend code.

### Reproduce locally

```powershell
cd src/backend
dotnet build
dotnet test

cd ../frontend
npm ci
npm run build
$env:CI = "true"; npm test
```

Stop any running `Shora.Api` process before building the backend — a running API locks output DLLs.

---

## Deploy (`deploy.yml`)

Runs on **every push to `main`**. There is no manual dispatch — merging to `main` is the only deploy trigger.

The deploy job fails if `RAILWAY_SERVICE_ID`, `PRODUCTION_URL`, or `RAILWAY_TOKEN` is missing (no silent skip).

**Concurrency:** only one Deploy run per branch at a time. A newer push to `main` **cancels** the in-progress run (build, Railway redeploy, or smoke test) via `cancel-in-progress: true` in [`deploy.yml`](deploy.yml). The latest commit is the only one that should finish and call `railway redeploy`.

### What the workflow does

1. **Build job** — `npm ci` + production Angular build → copy into `Shora.Api/wwwroot/` → `dotnet publish` → upload artifact (spec 09.8).
2. **Deploy job** — download artifact → build [`Dockerfile`](../Dockerfile) → push `ghcr.io/<lowercase-repo>:production` (repository path lowercased for GHCR) → `railway redeploy` (Path B-lite).
3. **Smoke-test job** — curl `/api/v1/health`, `/`, and `/about` on `PRODUCTION_URL`.

You can also open those URLs in a browser after deploy.

### One-time setup before first deploy

1. **Neon + Azure Storage** — PostgreSQL project + Blob storage (receipts): [docs/railway-prerequisites.md](../docs/railway-prerequisites.md) § B1.
2. **Railway** — project, service, domain, GHCR image link: same doc § B2–B4.
3. **GitHub → Settings → Environments** — create `production` (optional required reviewers).
4. **Repository variables:** `RAILWAY_SERVICE_ID` = `f69a711c-b830-4a97-a269-fa5e2b6f4dc9`, `PRODUCTION_URL` = `https://shora-production.up.railway.app`.
5. Optional **repository variable:** `DEPLOY_ENVIRONMENT` (defaults to `production`).
6. **Environment secret:** `RAILWAY_TOKEN` = Railway **project token** (project **shora** → Settings → Tokens → `production`). Not an account token.
7. Enable **branch protection** on `main` so CI passes before merge.

Google sign-in: set `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) before merge (build-time). Production builds use `fileReplacements` in [`angular.json`](../src/frontend/angular.json).

**Legacy Azure App Service path:** [docs/azure-prerequisites.md](../docs/azure-prerequisites.md) if migrating back when quota is approved.

---

## Dependabot

[`.github/dependabot.yml`](../dependabot.yml) opens **monthly** PRs for NuGet (`src/backend`) and npm (`src/frontend`) dependency updates (spec 09.4).

---

## Branch protection (09.4)

Configure once in GitHub after CI has passed at least once on `main`:

1. **Settings → Branches → Add rule** for `main`
2. **Require status checks to pass before merging** — select `Backend` and `Frontend`
3. **Require branches to be up to date before merging** (recommended)

This is a repository setting only — it cannot be committed to the repo.

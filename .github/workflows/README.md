# GitHub Actions workflows

CI/CD behavior for Shora. For hosting setup and secrets, see [docs/deployment.md](../docs/deployment.md). Full pipeline design: [specs/09-ci-cd-pipeline.md](../specs/09-ci-cd-pipeline.md).

## Overview

| Workflow   | File                       | Runs when                   | Deploys?                                                                   |
| ---------- | -------------------------- | --------------------------- | -------------------------------------------------------------------------- |
| **CI**     | [`ci.yml`](ci.yml)         | Every push and PR to `main` | No — validates code only                                                   |
| **Deploy** | [`deploy.yml`](deploy.yml) | Push to `main`              | Yes — builds publish artifact, pushes container to GHCR, redeploys Railway |

**CI** and **Deploy** serve different jobs. CI keeps `main` healthy; Deploy ships a release after Railway, Neon/Azure Storage, and GitHub secrets exist.

---

## CI (`ci.yml`)

| Job          | When it runs                                        | Working directory | Steps                                                 |
| ------------ | --------------------------------------------------- | ----------------- | ----------------------------------------------------- |
| **Changes**  | Always                                              | repo root         | `dorny/paths-filter@v3` — sets backend/frontend flags |
| **Backend**  | `src/backend/**` or `.github/workflows/**` changed  | `src/backend`     | `dotnet restore` → `build` → `test`                   |
| **Frontend** | `src/frontend/**` or `.github/workflows/**` changed | `src/frontend`    | `npm ci` → `npm run build` → `npm test` (`CI=true`)   |

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

**Concurrency:** only one Deploy run per branch at a time. A newer push to `main` **cancels** the in-progress run (build or Railway redeploy) via `cancel-in-progress: true` in [`deploy.yml`](deploy.yml). The latest commit is the only one that should finish and call `railway redeploy`.

### What the workflow does

1. **Build job** — `npm ci` + production Angular build → copy into `Shora.Api/wwwroot/` → `dotnet publish` → upload artifact.
2. **Deploy job** — download artifact → build [`Dockerfile`](../Dockerfile) → push `ghcr.io/<lowercase-repo>:production` (repository path lowercased for GHCR) → `railway redeploy`.

After deploy, verify manually: `GET /api/v1/health`, `/`, and `/about` on `PRODUCTION_URL`.

### One-time setup before first deploy

See [docs/deployment.md](../docs/deployment.md) for:

1. Neon PostgreSQL + Azure Blob storage
2. Railway project, service, domain, and Docker image source
3. Railway environment variables
4. GHCR pull access
5. GitHub `RAILWAY_*` secrets and variables
6. Branch protection on `main`

---

## Dependabot

[`.github/dependabot.yml`](../dependabot.yml) opens **monthly** PRs for NuGet (`src/backend`) and npm (`src/frontend`) dependency updates.

---

## Branch protection

Configure once in GitHub after CI has passed at least once on `main`:

1. **Settings → Branches → Add rule** for `main`
2. **Require status checks to pass before merging** — select `Backend` and `Frontend`
3. **Require branches to be up to date before merging** (recommended)

This is a repository setting only — it cannot be committed to the repo.

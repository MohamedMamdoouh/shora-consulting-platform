# GitHub Actions workflows

Full CI/CD design: [specs/09-ci-cd-pipeline.md](../specs/09-ci-cd-pipeline.md) · Production go-live: [docs/README.md](../docs/README.md)

## Overview

| Workflow | File | Runs when | Deploys? |
| --- | --- | --- | --- |
| **CI** | [`ci.yml`](ci.yml) | Every push and PR to `main` | No — validates code only |
| **Deploy** | [`deploy.yml`](deploy.yml) | Push to `main` | Yes — builds publish artifact and uploads to Azure App Service |

**CI** and **Deploy** serve different jobs. CI keeps `main` healthy; Deploy ships a release after Azure and GitHub secrets exist.

---

## CI (`ci.yml`)

| Job | When it runs | Working directory | Steps |
| --- | --- | --- | --- |
| **Changes** | Always | repo root | `dorny/paths-filter@v3` — sets backend/frontend flags |
| **Backend** | `src/backend/**` or `.github/workflows/**` changed | `src/backend` | `dotnet restore` → `build` → `test` |
| **Frontend** | `src/frontend/**` or `.github/workflows/**` changed | `src/frontend` | `npm ci` → `npm run build` → `npm test` (`CI=true`) |

- Backend tests use Testcontainers SQL Server (~240 xUnit methods; Docker required on the runner).
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

A **validate** step at the start of the deploy job fails the workflow if `AZURE_WEBAPP_NAME` is missing (no silent skip).

Concurrent pushes cancel the in-progress deploy (`concurrency` group) so only the latest `main` commit deploys.

### What the workflow does

1. **Build job** — `npm ci` + production Angular build → copy into `Shora.Api/wwwroot/` → `dotnet publish` → upload artifact (spec 09.8).
2. **Deploy job** — fail if `AZURE_WEBAPP_NAME` is unset → download artifact → `azure/webapps-deploy` to App Service (spec 09.9).
3. **Smoke-test job** — curl `/api/v1/health`, `/`, and `/about` on `https://<AZURE_WEBAPP_NAME>.azurewebsites.net`.

You can also open those URLs in a browser after deploy.

### One-time setup before first deploy

1. **Azure Portal** — [docs/azure-prerequisites.md](../docs/azure-prerequisites.md): App Service, SQL, Blob, application settings.
2. **GitHub → Settings → Environments** — create `production` (optional required reviewers).
3. **Repository variable:** `AZURE_WEBAPP_NAME` = your Web App name.
4. Optional **repository variable:** `DEPLOY_ENVIRONMENT` (defaults to `production`).
5. **Environment secret:** `AZURE_WEBAPP_PUBLISH_PROFILE` = publish profile from App Service → **Get publish profile**.
6. Enable **branch protection** on `main` so CI passes before merge.

Google sign-in: set `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) before merge (build-time; not injected in the workflow).

Optional later: OIDC federated credential instead of publish profile.

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

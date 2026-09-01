# GitHub Actions workflows

CI behavior for Shora. Production deploys are handled by **Render auto-deploy** on push to `main` — see [docs/deployment.md](../docs/deployment.md). Full pipeline design: [specs/09-ci-cd-pipeline.md](../specs/09-ci-cd-pipeline.md).

## Overview

| Workflow      | File                         | Runs when                              | Deploys?                 |
| ------------- | ---------------------------- | -------------------------------------- | ------------------------ |
| **CI**        | [`ci.yml`](ci.yml)           | Every push and PR to `main`            | No — validates code only |
| **Keep alive**| [`keep-alive.yml`](keep-alive.yml) | Every 14 min (UTC) + manual dispatch | No — pings production health |

Production releases: push/merge to `main` → Render builds from [`Dockerfile`](../Dockerfile) and deploys automatically.

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

### One-time setup before first production deploy

See [docs/deployment.md](../docs/deployment.md) for:

1. Supabase PostgreSQL + Azure Blob storage
2. Render web service (Git + Docker, `main` branch, auto-deploy)
3. Render environment variables
4. Branch protection on `main`

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

---

## Keep alive (`keep-alive.yml`)

| Job    | When it runs                         | Steps                                      |
| ------ | ------------------------------------ | ------------------------------------------ |
| **Ping** | Every 14 minutes (UTC) + manual run | `curl` → `https://$PRODUCTION_HOST/api/v1/health` |

Requires repository variable `PRODUCTION_HOST` (hostname only, e.g. `shora.onrender.com`). See [docs/deployment.md §5](../docs/deployment.md#5-free-tier-keep-alive).

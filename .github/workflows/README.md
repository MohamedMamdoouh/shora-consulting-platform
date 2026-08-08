# GitHub Actions workflows

Full CI/CD design: [specs/09-ci-cd-pipeline.md](../specs/09-ci-cd-pipeline.md)

## CI (`ci.yml`)

Runs on every push and pull request to `main`:

| Job          | When it runs                                      | Working directory | Steps                                                    |
| ------------ | ------------------------------------------------- | ----------------- | -------------------------------------------------------- |
| **Changes**  | Always                                            | repo root         | `dorny/paths-filter@v3` — sets backend/frontend flags    |
| **Backend**  | `src/backend/**` or `.github/workflows/**` changed | `src/backend`     | `dotnet restore` → `build` → `test`                      |
| **Frontend** | `src/frontend/**` or `.github/workflows/**` changed | `src/frontend`    | `npm ci` → `npm run build` → `npm test` (with `CI=true`) |

Backend tests use Testcontainers SQL Server (Docker required on the runner). ~240 xUnit test methods.

## Reproduce locally

```powershell
cd src/backend
dotnet build
dotnet test

cd ../frontend
npm ci
npm run build
$env:CI = "true"; npm test
```

Stop any running `Shora.Api` process before building the backend.

## Phase 2 (CD)

Deploy workflow is not implemented yet. See spec 09 #5–#6 for the planned Azure App Service rollout.

## Dependabot

`.github/dependabot.yml` opens weekly PRs for NuGet (`src/backend`) and npm (`src/frontend`) dependency updates (spec 09 §4).

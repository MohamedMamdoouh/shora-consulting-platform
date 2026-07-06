# GitHub Actions workflows

Full CI/CD design: [specs/09-ci-cd-pipeline.md](../specs/09-ci-cd-pipeline.md)

## CI (`ci.yml`)

Runs on every push and pull request to `main`:

| Job | Working directory | Steps |
|-----|-------------------|-------|
| **Backend** | `src/backend` | `dotnet restore` → `build` → `test` |
| **Frontend** | `src/frontend` | `npm ci` → `npm run build` → `npm test` (with `CI=true`) |

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

Deploy workflow is not implemented yet. See spec 09 §5–§6 for the planned Azure App Service rollout.

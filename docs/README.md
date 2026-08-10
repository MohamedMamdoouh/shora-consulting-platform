# Operator documentation

Pointers for running Shora in production. Application code lives under `src/`; these docs cover hosting and configuration only.

| Doc | When to use |
| --- | --- |
| [azure-prerequisites.md](azure-prerequisites.md) | **First-time setup** — Azure resources, GitHub secrets, go-live checklist (spec 09.7) |
| [production-config.md](production-config.md) | **App settings** — connection strings, JWT, SMTP, OAuth, custom domain, admin bootstrap (spec 09.6) |
| [ops-runbooks.md](ops-runbooks.md) | **Incidents** — ops alert runbooks (spec 08.9) |

## Azure MCP plugin (Cursor)

Use the **Azure MCP** plugin in Cursor to provision resources, set App Service settings, and troubleshoot — instead of repo scripts or Infrastructure-as-Code in git.

1. **Settings → MCP** — ensure the Azure plugin is enabled.
2. In chat, ask the agent to **authenticate Azure MCP** (`mcp_auth`), then complete browser sign-in.
3. Example prompts:
   - *"List my Azure subscriptions and create Shora production resources: Linux App Service (.NET 10, Always On, 1 instance), Azure SQL Basic, storage account with private `receipts` container."*
   - *"Set App Service application settings for `<webapp>` from [production-config.md](production-config.md)."*
   - *"Why did `/api/v1/health` fail after deploy?"* (diagnostics via App Service / App Lens tools)

You can also use the **Azure Portal** manually — same checklist in [azure-prerequisites.md](azure-prerequisites.md).

## Go-live order (spec 09)

### A. Azure (Portal or Azure MCP)

1. Provision App Service (Linux .NET 10, Always On, **1 instance**), Azure SQL, and Blob storage — [azure-prerequisites.md](azure-prerequisites.md).
2. Set App Service application settings — [production-config.md](production-config.md).

### B. Google (if using sign-in)

3. Google Cloud Console → OAuth client → Authorized JavaScript origins = production HTTPS URL — [production-config.md § Google Cloud setup](production-config.md#google-cloud-setup).
4. Set `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts) and merge to `main` (build-time).

### C. GitHub

5. Repository variable `AZURE_WEBAPP_NAME`; environment secret `AZURE_WEBAPP_PUBLISH_PROFILE` on `production`; optional `DEPLOY_ENVIRONMENT` — [azure-prerequisites.md § GitHub](azure-prerequisites.md#github-spec-099).
6. Branch protection on `main` — require CI `Backend` and `Frontend` before merge — [`.github/workflows/README.md`](../.github/workflows/README.md).

### D. First deploy

7. Merge to `main` — **Deploy** runs automatically (no manual workflow).
8. Confirm Deploy workflow **smoke-test** passes, or open `https://<host>/` and `/api/v1/health` manually; log in as admin; set real payment settings; test signup → verify email → book → receipt → approve.
9. Remove `AdminSeed__*` from App Service after admin account works.
10. Custom domain — [production-config.md § Custom domain](production-config.md#custom-domain).

## CI vs deploy (what runs when)

| Workflow | File | Trigger | Purpose |
| --- | --- | --- | --- |
| **CI** | [ci.yml](../.github/workflows/ci.yml) | Every push/PR to `main` | Build + test backend and frontend separately |
| **Deploy** | [deploy.yml](../.github/workflows/deploy.yml) | Push to `main` | Build combined publish artifact + upload to Azure |

CI validates on PRs; **Deploy runs automatically when changes land on `main`** (after Azure + GitHub secrets are configured). Enable branch protection so only CI-green PRs merge.

Design detail: [spec 09](../specs/09-ci-cd-pipeline.md).

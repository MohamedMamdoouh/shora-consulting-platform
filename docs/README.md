# Operator documentation

Pointers for running Shora in production. Application code lives under `src/`; these docs cover hosting and configuration only.

| Doc | When to use |
| --- | --- |
| [azure-prerequisites.md](azure-prerequisites.md) | **First-time setup** — Azure resources, GitHub secrets, go-live checklist (spec 09.7) |
| [production-config.md](production-config.md) | **App settings** — connection strings, JWT, SMTP, OAuth, admin bootstrap (spec 09.6) |
| [custom-domain.md](custom-domain.md) | **Custom domain + TLS** — DNS, certificate, URL settings |
| [ops-runbooks.md](ops-runbooks.md) | **Incidents** — ops alert runbooks (spec 08.9) |

## Automation scripts

| Script | Purpose |
| --- | --- |
| [`scripts/provision-azure.ps1`](../scripts/provision-azure.ps1) | Deploy [`infra/`](../infra/) Bicep (App Service + SQL + Blob) |
| [`scripts/set-app-settings.ps1`](../scripts/set-app-settings.ps1) | Apply App Service application settings |
| [`scripts/configure-github.ps1`](../scripts/configure-github.ps1) | Set GitHub variables/secrets for Deploy workflow |
| [`scripts/post-deploy-verify.ps1`](../scripts/post-deploy-verify.ps1) | Smoke-test health, SPA, and public settings API |

## Go-live order (spec 09)

### A. Azure (Portal or script)

1. Provision resources — Portal checklist in [azure-prerequisites.md](azure-prerequisites.md), or:

   ```powershell
   .\scripts\provision-azure.ps1 -BaseName shora -Location westeurope -SqlAdminPassword 'YourStrongP@ssw0rd!'
   ```

2. Apply app settings — [production-config.md](production-config.md) or `scripts/set-app-settings.ps1`.

### B. Google (if using sign-in)

3. Google Cloud Console → OAuth client → Authorized JavaScript origins = production HTTPS URL — [production-config.md § Google Cloud setup](production-config.md#google-cloud-setup).
4. Set GitHub variable `GOOGLE_CLIENT_ID` (injected at build in [`deploy.yml`](../.github/workflows/deploy.yml)) or commit `googleClientId` in [`environment.production.ts`](../src/frontend/src/environments/environment.production.ts).

### C. GitHub

5. Run `scripts/configure-github.ps1` or set manually: `AZURE_WEBAPP_NAME`, `AZURE_WEBAPP_PUBLISH_PROFILE`, optional `GOOGLE_CLIENT_ID` — [azure-prerequisites.md § GitHub](azure-prerequisites.md#github-spec-099).
6. Branch protection on `main` — require CI `Backend` and `Frontend` before merge — [`.github/workflows/README.md`](../.github/workflows/README.md).

### D. First deploy

7. Merge to `main` — **Deploy** runs automatically (no manual workflow).
8. Run `scripts/post-deploy-verify.ps1 -BaseUrl https://<host>` or rely on Deploy workflow smoke-test job; log in as admin; set real payment settings; test signup → verify email → book → receipt → approve.
9. Remove `AdminSeed__*` from App Service after admin account works.
10. Custom domain — [custom-domain.md](custom-domain.md).

## CI vs deploy (what runs when)

| Workflow | File | Trigger | Purpose |
| --- | --- | --- | --- |
| **CI** | [ci.yml](../.github/workflows/ci.yml) | Every push/PR to `main` | Build + test backend and frontend separately |
| **Deploy** | [deploy.yml](../.github/workflows/deploy.yml) | Push to `main` | Build combined publish artifact + upload to Azure |

CI validates on PRs; **Deploy runs automatically when changes land on `main`** (after Azure + GitHub secrets are configured). Enable branch protection so only CI-green PRs merge.

Design detail: [spec 09](../specs/09-ci-cd-pipeline.md).

# Shora

[![CI](https://github.com/MohamedMamdoouh/shora-consulting-platform/actions/workflows/ci.yml/badge.svg)](https://github.com/MohamedMamdoouh/shora-consulting-platform/actions/workflows/ci.yml)

Arabic-first (RTL) relationship consulting booking platform. Implementation follows numbered specs in [`specs/`](specs/).

## Repository layout

```text
Shora/
├── specs/                # Spec-driven documentation (00–09)
├── docs/                 # Operator docs (Azure, production config, runbooks)
├── src/
│   ├── contracts/        # TypeScript API contracts
│   ├── backend/          # .NET 10 Clean Architecture API
│   └── frontend/         # Angular 21 app
├── .github/workflows/    # CI + deploy workflows (spec 09)
├── .gitignore
└── README.md
```

## Prerequisites

- .NET 10 SDK
- Node.js 22+ (Angular CLI 21 used; Angular 22 requires Node 22.22.3+)
- SQL Server LocalDB (or full SQL Server) for backend database
- Docker Desktop (or Docker Engine) for backend tests (`dotnet test` uses Testcontainers SQL Server)

## Backend setup

```powershell
cd src/backend
dotnet restore
dotnet build
dotnet ef database update --project Shora.Infrastructure --startup-project Shora.Api
dotnet run --project Shora.Api
```

Default dev URLs: `https://localhost:7183` / `http://localhost:5107`

### Configuration

- Connection string: `ConnectionStrings:DefaultConnection` in `appsettings.Development.json`
- Admin seed (dev only): `AdminSeed:Email` and `AdminSeed:Password` in `appsettings.Development.json`
- Receipt blob storage (dev): `Storage:ConnectionString` defaults to `UseDevelopmentStorage=true` in `appsettings.Development.json` — requires [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) running locally:

  ```powershell
  docker run --rm -p 10000:10000 mcr.microsoft.com/azure-storage/azurite:3.35.0 azurite-blob --blobHost 0.0.0.0 --blobPort 10000 --skipApiVersionCheck
  ```

  Production: set `Storage:ConnectionString` and `Storage:ReceiptContainer` via `dotnet user-secrets` or environment variables (private Azure Blob container; never commit credentials).

- Production secrets: use `dotnet user-secrets` or environment variables — never commit real credentials
- Production email: configure `Email:Host`, `Email:Port`, `Email:Username`, `Email:Password`, `Email:FromAddress` (spec 08.4); when unset in non-dev environments, emails are no-ops

On startup, migrations apply automatically and seed data runs idempotently:

- `Client` and `Admin` roles
- Singleton `Settings` row (`Id = 1`, 500 EGP defaults)
- Admin user when `AdminSeed` credentials are configured

### Deployment topology

Production runs on a **single always-on server** (Azure App Service):

- One process hosts the API, Angular static files (`wwwroot`), in-process background jobs, and in-memory cache
- No load balancer, no horizontal scaling, and no distributed cache (Redis)
- Azure SQL and Azure Blob Storage are the external services

**Go-live (operator):** [docs/README.md](docs/README.md) — Azure Portal setup, app settings, GitHub Deploy workflow.

### Health check

`GET /api/v1/health` — returns `{ status, timestampUtc }`

OpenAPI document (Development): `/openapi/v1.json`

### API conventions

All endpoints follow [spec 00](specs/00-api-conventions.md): Result pattern, Problem Details errors, API v1 routes, shared contracts (`Shora.Contracts` + `src/contracts/`), and central NuGet versions via `Directory.Build.props` / `Directory.Packages.props`.

## Frontend setup

```powershell
cd src/frontend
npm install
npm start
```

Default: `http://localhost:4200` (RTL Arabic app with lazy-loaded routes)

Implemented routes:

| Area             | Routes                     | Spec                  |
| ---------------- | -------------------------- | --------------------- |
| Public           | `/`, `/about`, `/services` | 03 (placeholder copy) |
| Auth             | `/auth/*`                  | 02                    |
| Booking          | `/booking/*`               | 04                    |
| Client dashboard | `/dashboard`               | 06                    |
| Admin            | `/admin/*`                 | 07                    |

API base URL: `src/environments/environment.ts` → `/api/v1` (proxied to the backend in dev — see below)

### Dev proxy (auth cookies)

During local development, run the frontend with `npm start`. The dev server proxies `/api` to `https://localhost:7183` so the browser treats API calls as same-origin on `localhost:4200`. This is required for refresh-token cookies (`SameSite=Strict`).

- Use `http://localhost:4200` in the browser — not `https://localhost:7183` directly — when testing login, refresh, or logout.
- Refresh cookies use `Secure=false` in Development only (Angular dev server is HTTP).

## Tests

Same commands as [CI](.github/workflows/ci.yml) — see [spec 09](specs/09-ci-cd-pipeline.md) for the full pipeline design.

**Backend tests** require Docker to be running. They use an ephemeral SQL Server container (Testcontainers), separate from the LocalDB instance used by `dotnet run`.

```powershell
cd src/backend
dotnet build
dotnet test   # ~240 xUnit tests; requires Docker (Testcontainers)

cd ../frontend
npm ci
npm run build
$env:CI = "true"; npm test
```

## Production deployment

All pipeline **code** is in the repo (spec 09). Going live is **operator work** — no separate staging environment:

1. [docs/azure-prerequisites.md](docs/azure-prerequisites.md) — Azure Portal or **Azure MCP** plugin in Cursor
2. [docs/production-config.md](docs/production-config.md) — App Service application settings
3. GitHub — repository variables + environment secrets; merges to `main` auto-deploy

Full ordered checklist: [docs/README.md](docs/README.md).

## Spec implementation roadmap

| Spec | Area                                                 | Status                                                       |
| ---- | ---------------------------------------------------- | ------------------------------------------------------------ |
| 00   | API conventions (Result, Problem Details, contracts) | **Done**                                                     |
| 01   | Project scaffold & data model                        | **Done**                                                     |
| 02   | Authentication (JWT, Google, refresh tokens)         | **Done**                                                     |
| 03   | Public pages (Home, About, Services)                 | **Done** (launch-ready Arabic copy + branding assets) |
| 04   | Booking flow                                         | **Done**                                                     |
| 05   | Manual payments (Vodafone Cash / InstaPay receipts)  | **Done**                                                     |
| 06   | Client dashboard                                     | **Done** (06a–06j)                                           |
| 07   | Admin dashboard                                      | **Done** (07a–07o; ops alerts UI optional)                   |
| 08   | Cross-cutting concerns (jobs, rate limits, ops)      | **Done** (08.1–08.9)                                         |
| 09   | CI/CD pipeline (GitHub Actions + Azure deploy)       | **Done** in repo — [go-live checklist](docs/README.md) for Azure + GitHub secrets |

Implement specs **01–08** in order for new features — the MVP backend and dashboards are **complete** except public-page content (03) and optional ops alerts UI. Spec **09** code is complete; remaining work is operational: provision Azure and run the first **Deploy** workflow ([docs/README.md](docs/README.md)).

## Architecture (backend)

```text
Api → Application → Domain
Infrastructure → Application + Domain
Contracts (shared DTOs, referenced by Api + frontend TS mirrors)
```

- **Domain:** entities, enums, invariants (no EF/ASP.NET dependencies)
- **Application:** use-case services, `Result` pattern, `IApplicationDbContext`, background-job services, outbox email renderer, ops monitoring
- **Infrastructure:** EF Core, Identity stores, seed, Azure Blob file storage (`IFileStorage`), SMTP email (`SmtpEmailSender`), pass-through malware scanner stub
- **Contracts:** shared request/response DTOs (C# records in `Shora.Contracts`; TypeScript mirrors in `src/contracts/`)
- **Api:** controllers, Problem Details, global exception handler, API versioning (`/api/v1/...`), rate limiting, correlation ID middleware, in-process background jobs

## Spec 05 — payment backend (complete)

Backend API for manual payment verification is **complete** (sub-phases 05a–05g):

- Azure Blob receipt storage + Azurite local dev
- Client receipt upload with validation and anti-replay (duplicate hash warnings)
- Admin receipt review (SAS URLs gated on `Clean` malware scan)
- Admin approve/decline with outbox emails
- Manual refund record/revoke (`/api/v1/admin/payments/{id}/refunds/*`)
- Receipt upload rate limit: 5/min/account (part of full rate-limit matrix in spec 08)

**Blob reconciliation (05h, spec 08.6):** `ReceiptBlobReconciliationService` repairs `BlobFinalizePending`/`Missing` rows and deletes orphan `temp/` blobs.

**Client payment UX (spec 06):** `/dashboard` pending cards and `/booking/payment/:id` share `PaymentInstructionsPanelComponent` (instructions, countdown, receipt upload, cancel hold).

**Admin payment UX (spec 07):** receipt review modal, refund record/revoke, and earnings summary on `/admin/bookings` and `/admin/earnings`.

## Spec 06 — client dashboard (complete)

Sub-phases 06a–06j:

- `GET /api/v1/bookings/mine` with status filters, past pagination, and enriched list items (payment summary, receipt thumbnail SAS, cancellation metadata, consultant WhatsApp)
- `/dashboard` — upcoming (delivery + cancellation UX), pending payment/approval cards, past history with Arabic reason/refund labels and load-more
- Shared payment panel reused by standalone payment-instructions page

See [spec 06](specs/06-client-dashboard.md) for full detail.

## Spec 07 — admin dashboard (complete)

Sub-phases 07a–07n:

- `/admin/settings` — session price, duration, payment numbers, validation
- `/admin/availability` — recurring windows + blocked dates
- `/admin/bookings` — paginated table, receipt review, cancellation queue, direct cancel, refund record/revoke
- `/admin/earnings` — gross / refunded / net revenue and refund-due counts (date filter on session slot)

See [spec 07](specs/07-admin-dashboard.md) for full detail.

## Spec 08 — cross-cutting concerns (complete)

Sub-phases 08.1–08.9:

- **Observability:** correlation ID middleware, payment log scopes, `JobRunHistory` + job heartbeats
- **Email:** HTML transaction templates, outbox dispatcher (8-attempt retry, dead-letter), production SMTP (`MailKit`)
- **Background jobs:** receipt-deadline cleanup, outbox dispatcher, cancellation auto-decline, booking auto-complete, blob reconciliation, refresh-token purge, temp blob cleanup, receipt retention purge, availability top-up, ops monitoring
- **Rate limiting:** auth, availability, booking reserve, receipt upload, cancellation request (configurable via `RateLimiting` + `ReceiptUpload`)
- **Ops monitoring:** `OpsMonitoringService` + `GET /api/v1/admin/ops/alerts`; runbooks in [`runbooks.json`](src/backend/Shora.Application/Ops/runbooks.json), admin UI at `/admin/ops`, API at `GET /admin/ops/runbooks`

Disable background jobs in tests via `BackgroundJobs:Enabled = false`.

See [spec 08](specs/08-cross-cutting-concerns.md) for intervals, thresholds, and deployment notes.

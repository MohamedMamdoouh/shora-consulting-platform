# Shora

Arabic-first (RTL) relationship consulting booking platform. Implementation follows numbered specs in [`specs/`](specs/).

## Repository layout

```text
Shora/
├── specs/                # Spec-driven documentation (00–09)
├── src/
│   ├── contracts/        # TypeScript API contracts
│   ├── backend/          # .NET 10 Clean Architecture API
│   └── frontend/         # Angular 21 app
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

On startup, migrations apply automatically and seed data runs idempotently:

- `Client` and `Admin` roles
- Singleton `Settings` row (`Id = 1`, 500 EGP defaults)
- Admin user when `AdminSeed` credentials are configured

### Deployment topology

Production runs on a **single always-on server** (Azure App Service or equivalent):

- One process hosts the API, in-process background jobs, and in-memory cache
- No load balancer, no horizontal scaling, and no distributed cache (Redis)
- Azure SQL and Azure Blob Storage remain the shared external services

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

Default: `http://localhost:4200` (RTL Arabic shell with lazy-loaded route stubs)

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
dotnet test

cd ../frontend
npm ci
npm run build
$env:CI = "true"; npm test
```

## Spec implementation roadmap

| Spec | Area                                                 | Status          |
| ---- | ---------------------------------------------------- | --------------- |
| 00   | API conventions (Result, Problem Details, contracts) | **Done**        |
| 01   | Project scaffold & data model                        | **Done**        |
| 02   | Authentication (JWT, Google, refresh tokens)         | **Done**        |
| 03   | Public pages (Home, About, Services)                 | Planned         |
| 04   | Booking flow                                         | **In progress** |
| 05   | Manual payments (Vodafone Cash / InstaPay receipts)  | **Done** (API; UI in 06–07) |
| 06   | Client dashboard                                     | **In progress** (payment-instructions + upload; full dashboard planned) |
| 07   | Admin dashboard                                      | **In progress** (payment/refund APIs done; admin UI planned) |
| 08   | Cross-cutting concerns (jobs, rate limits, ops)      | **In progress** |
| 09   | CI/CD pipeline (GitHub Actions; Azure CD later)      | **In progress** |

Implement feature specs **in order** (01–08) — each builds on the previous. Spec 09 (CI/CD) runs in parallel with feature work.

## Architecture (backend)

```text
Api → Application → Domain
Infrastructure → Application + Domain
```

- **Domain:** entities, enums, invariants (no EF/ASP.NET dependencies)
- **Application:** use-case services, `Result` pattern, `IApplicationDbContext`, abstraction interfaces
- **Infrastructure:** EF Core, Identity stores, seed, Azure Blob file storage (`IFileStorage`), pass-through malware scanner stub, dev logging email
- **Contracts:** shared request/response DTOs (C# records)
- **Api:** controllers, Problem Details, global exception handler, API versioning, DI wiring

## Spec 05 — payment backend (complete)

Backend API for manual payment verification is **complete** (sub-phases 05a–05g):

- Azure Blob receipt storage + Azurite local dev
- Client receipt upload with validation and anti-replay (duplicate hash warnings)
- Admin receipt review (SAS URLs gated on `Clean` malware scan)
- Admin approve/decline with outbox emails
- Manual refund record/revoke (`/api/v1/admin/payments/{id}/refunds/*`)
- Receipt upload rate limit: 5/min/account

**Deferred to spec 08:** blob reconciliation job for `BlobFinalizePending` rows (05h). Uploads already mark the state; repair is ops polish.

Polished payment UX lives in specs 06 (client) and 07 (admin).

## Deferred / remaining

- Outbox email dispatcher (spec 08)
- Full rate-limit matrix for auth/booking endpoints (spec 08)
- Blob reconciliation job (spec 05h → 08)
- Feature pages and dashboards (specs 03, 06–07)
- Admin cancellation decisions and direct cancel (spec 07)

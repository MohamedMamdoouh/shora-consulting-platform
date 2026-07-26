# Shora

Arabic-first (RTL) relationship consulting booking platform. Implementation follows numbered specs in.

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
- Production secrets: use `dotnet user-secrets` or environment variables — never commit real credentials

On startup, migrations apply automatically and seed data runs idempotently:

- `Client` and `Admin` roles
- Singleton `Settings` row (`Id = 1`, 500 EGP defaults)
- Admin user when `AdminSeed` credentials are configured

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
| 04   | Booking flow                                         | Planned         |
| 05   | Manual payments (Vodafone Cash / InstaPay receipts)  | Planned         |
| 06   | Client dashboard                                     | Planned         |
| 07   | Admin dashboard                                      | Planned         |
| 08   | Cross-cutting concerns (jobs, rate limits, ops)      | Planned         |
| 09   | CI/CD pipeline (GitHub Actions; Azure CD later)      | **In progress** |

Implement feature specs **in order** (01–08) — each builds on the previous. Spec 09 (CI/CD) runs in parallel with feature work.

## Architecture (backend)

```text
Api → Application → Domain
Infrastructure → Application + Domain
```

- **Domain:** entities, enums, invariants (no EF/ASP.NET dependencies)
- **Application:** use-case services, `Result` pattern, `IApplicationDbContext`, abstraction interfaces
- **Infrastructure:** EF Core, Identity stores, seed, stub email/file providers
- **Contracts:** shared request/response DTOs (C# records)
- **Api:** controllers, Problem Details, global exception handler, API versioning, DI wiring

## Deferred from spec 01

- Background jobs, outbox dispatcher, rate limiting (spec 08)
- Azure Blob receipt storage (spec 05)
- Feature pages and business logic in specs 03–07

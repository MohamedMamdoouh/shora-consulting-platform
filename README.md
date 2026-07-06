# Shora

Arabic-first (RTL) relationship consulting booking platform. Implementation follows numbered specs in `[specs/](specs/)`.

## Repository layout

```text
Shora/
├── specs/                # Spec-driven documentation (01–08)
├── src/
│   ├── backend/          # .NET 10 Clean Architecture API
│   └── frontend/         # Angular 21 app (package name: shora-web)
├── .gitignore
└── README.md
```

## Prerequisites

- .NET 10 SDK
- Node.js 22+ (Angular CLI 21 used; Angular 22 requires Node 22.22.3+)
- SQL Server LocalDB (or full SQL Server) for backend database

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

On startup (non-test environments), migrations apply automatically and seed data runs idempotently:

- `Client` and `Admin` roles
- Singleton `Settings` row (`Id = 1`, 500 EGP defaults)
- Admin user when `AdminSeed` credentials are configured

### Health check

`GET /api/health` — returns `{ status, timestampUtc }`

OpenAPI document (Development): `/openapi/v1.json`

## Frontend setup

```powershell
cd src/frontend
npm install
npm start
```

Default: `http://localhost:4200` (RTL Arabic shell with lazy-loaded route stubs)

API base URL: `src/environments/environment.ts` → `https://localhost:7183/api`

## Tests

```powershell
cd src/backend
dotnet test
```

## Spec implementation roadmap

| Spec | Area                                                | Status   |
| ---- | --------------------------------------------------- | -------- |
| 01   | Project scaffold & data model                       | **Done** |
| 02   | Authentication (JWT, Google, refresh tokens)        | Planned  |
| 03   | Public pages (Home, About, Services)                | Planned  |
| 04   | Booking flow                                        | Planned  |
| 05   | Manual payments (Vodafone Cash / InstaPay receipts) | Planned  |
| 06   | Client dashboard                                    | Planned  |
| 07   | Admin dashboard                                     | Planned  |
| 08   | Cross-cutting concerns (jobs, rate limits, ops)     | Planned  |

Implement specs **in order** — each builds on the previous.

## Architecture (backend)

```text
Api → Application → Domain
Infrastructure → Application + Domain
```

- **Domain:** entities, enums, invariants (no EF/ASP.NET dependencies)
- **Application:** use-case services, `IApplicationDbContext`, abstraction interfaces
- **Infrastructure:** EF Core, Identity stores, seed, stub email/file providers
- **Api:** controllers, DI wiring, Identity registration (auth flows completed in spec 02)

## Deferred from spec 01

- JWT auth flows, Google login, refresh token rotation (spec 02)
- Background jobs, outbox dispatcher, rate limiting (spec 08)
- Azure Blob receipt storage (spec 05)
- Feature pages and business logic in specs 03–07

# Shora

[![CI](https://github.com/MohamedMamdoouh/shora-consulting-platform/actions/workflows/ci.yml/badge.svg)](https://github.com/MohamedMamdoouh/shora-consulting-platform/actions/workflows/ci.yml)

RTL personal practice site for one-to-one relationship consulting sessions. Clients browse availability, reserve a session, pay by manual bank transfer (Vodafone Cash or InstaPay), upload a receipt, and receive the session by voice call or chat. A single **Admin** (the practitioner) manages settings, availability, receipt approval, cancellations, and refunds.

**Status:** MVP feature set is implemented in code (backend, frontend, CI/CD). Production hosting uses Render + Neon PostgreSQL + Azure Blob Storage — see [Deployment](#23-deployment).

---

## Table of contents

1. [Project overview](#1-project-overview)
2. [Features](#2-features)
3. [Technology stack](#3-technology-stack)
4. [Repository structure](#4-repository-structure)
5. [Architecture](#5-architecture)
6. [Backend architecture](#6-backend-architecture)
7. [Frontend architecture](#7-frontend-architecture)
8. [Authentication & authorization](#8-authentication--authorization)
9. [Database](#9-database)
10. [Domain model & business rules](#10-domain-model--business-rules)
11. [Important workflows](#11-important-workflows)
12. [Payments](#12-payments)
13. [File storage](#13-file-storage)
14. [Email / notifications](#14-email--notifications)
15. [Background jobs](#15-background-jobs)
16. [API](#16-api)
17. [Configuration](#17-configuration)
18. [Local development setup](#18-local-development-setup)
19. [Development workflow](#19-development-workflow)
20. [Testing](#20-testing)
21. [Docker](#21-docker)
22. [CI/CD](#22-cicd)
23. [Deployment](#23-deployment)
24. [Security](#24-security)
25. [Error handling & observability](#25-error-handling--observability)
26. [Timezones & date/time handling](#26-timezones--date-time-handling)
27. [Important design decisions](#27-important-design-decisions)
28. [Common pitfalls](#28-common-pitfalls)
29. [Troubleshooting](#29-troubleshooting)
30. [Useful commands](#30-useful-commands)
31. [Project conventions](#31-project-conventions)
32. [Glossary](#32-glossary)
33. [Future improvements / known limitations](#33-future-improvements--known-limitations)

---

## 1. Project overview

| Item                 | Description                                                                                                                  |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| **Name**             | Shora                                                                                                                        |
| **Purpose**          | Personal practice booking site for one-to-one relationship consulting sessions                                               |
| **Problem**          | Lets a single practitioner offer bookable sessions with manual local payment verification, without an online payment gateway |
| **Target users**     | **Clients** — people booking sessions; **Admin** — the practitioner operating their personal practice                        |
| **Language / UX**    | RTL UI (`lang="ar"`), mobile-first public pages                                                                              |
| **Deployment model** | Single container: API + Angular SPA on one origin (required for auth cookies)                                                |

Shora is a monorepo: .NET 10 backend (Clean Architecture), Angular 21 frontend, shared TypeScript/C# contracts, and GitHub Actions CI/CD.

Design specs for maintainers live in [`specs/`](specs/). Operator deployment details live in [`docs/deployment.md`](docs/deployment.md).

---

## 2. Features

| Feature                    | What it does                                                                          |
| -------------------------- | ------------------------------------------------------------------------------------- |
| **Public marketing pages** | Home, About, Services, Privacy, Terms — CTAs into booking without a login wall        |
| **Availability browsing**  | Clients see open slots in a date range (cached public API)                            |
| **Multi-step booking**     | Pick slot → delivery method (voice/chat) → phone (voice only) → review → reserve      |
| **Account & auth**         | Email/password signup, login, email verification, password reset                      |
| **Manual payments**        | Client transfers fee off-platform, uploads receipt image; admin approves or declines  |
| **Client dashboard**       | Upcoming, pending payment/approval, and paginated past bookings                       |
| **Cancellation requests**  | Client requests cancellation; admin approves/declines; auto-decline near session time |
| **Admin settings**         | Session price, duration, buffer, payment numbers, WhatsApp, upload windows            |
| **Admin availability**     | Recurring weekly windows and blocked date ranges                                      |
| **Admin bookings**         | Filterable list, receipt review, direct cancel, cancellation queue                    |
| **Refunds**                | Admin records manual refunds (no payment provider integration)                        |
| **Earnings**               | Gross / refunded / net revenue summary with date filters                              |
| **Ops monitoring**         | Background health checks with admin alerts and runbooks at `/admin/ops`               |
| **Transactional email**    | Auth emails (direct Brevo) + booking/payment emails (outbox with retry)               |
| **Rate limiting**          | Per-endpoint per-IP limits on auth, availability, booking, receipts, cancellations    |
| **CI/CD**                  | Path-filtered CI on PRs; Render auto-deploy on push to `main`                       |

---

## 3. Technology stack

| Area                 | Technology                                                    | Purpose                                    |
| -------------------- | ------------------------------------------------------------- | ------------------------------------------ |
| Backend runtime      | .NET 10                                                       | ASP.NET Core Web API host                  |
| Backend architecture | Clean Architecture (Domain, Application, Infrastructure, Api) | Separation of concerns, testability        |
| ORM                  | EF Core 10 + Npgsql                                           | PostgreSQL data access, migrations         |
| Database             | PostgreSQL 16+ (Neon in production)                           | Primary data store                         |
| Identity             | ASP.NET Core Identity                                         | Users, roles, password hashing             |
| Authentication       | JWT Bearer + httpOnly refresh cookie                          | Access token in memory; refresh via cookie |
| Frontend             | Angular 21 (standalone components)                            | SPA, lazy routes, Vitest                   |
| API contracts        | `Shora.Contracts` (C#) + `src/contracts` (TS)                 | Shared DTO shapes, manual sync             |
| File storage         | Azure Blob Storage (Azurite locally)                          | Private receipt images                     |
| Email (dev)          | `DevLoggingEmailSender`                                       | Logs emails to console                     |
| Email (prod)         | Brevo HTTPS API                                               | Auth + transactional mail                  |
| Caching              | In-memory cache + ASP.NET Output Cache                        | Public settings & availability             |
| Rate limiting        | ASP.NET Core Rate Limiter                                     | Abuse protection                           |
| Testing              | xUnit v3, Testcontainers (PostgreSQL, Azurite)                | Unit + integration tests                   |
| CI/CD                | GitHub Actions (`ci.yml`)                                     | Tests on PR/push; deploy on Render |
| Container            | Docker multi-stage ([`Dockerfile`](Dockerfile))               | Built on Render from Git           |
| Hosting              | Render                                                        | Compute; serves API + `wwwroot` SPA |

**Not used:** Stripe or other online payment gateways, Redis, message queues, docker-compose, separate staging environment.

---

## 4. Repository structure

```text
Shora/
├── .github/
│   ├── workflows/          # CI GitHub Actions
│   └── dependabot.yml      # Monthly NuGet/npm updates
├── docs/
│   ├── deployment.md       # Production hosting guide
│   ├── ops-runbooks.md     # Ops runbook pointer
│   └── README.md           # Operator doc index
├── specs/                  # Feature design docs (00–09)
├── src/
│   ├── backend/            # .NET solution (Shora.slnx)
│   │   ├── Shora.Api/              # HTTP host, controllers, middleware, jobs
│   │   ├── Shora.Application/      # Business logic, validators, email templates
│   │   ├── Shora.Domain/           # Entities, enums
│   │   ├── Shora.Contracts/        # Shared request/response DTOs
│   │   ├── Shora.Infrastructure/   # EF Core, Identity, Brevo email, Blob, seed
│   │   └── Shora.Tests/            # xUnit tests
│   ├── contracts/          # TypeScript mirrors of Shora.Contracts
│   └── frontend/           # Angular app (shora-web)
├── Dockerfile              # Multi-stage production image (Render builds from Git)
└── README.md
```

| Path                               | Purpose                                                           |
| ---------------------------------- | ----------------------------------------------------------------- |
| `src/backend/Shora.Api`            | Entry point, DI wiring, background job hosts, `Program.cs`        |
| `src/backend/Shora.Application`    | Use-case services, outbox, ops monitoring, options classes        |
| `src/backend/Shora.Domain`         | Pure domain model — no EF or ASP.NET references                   |
| `src/backend/Shora.Infrastructure` | PostgreSQL, Identity stores, JWT/refresh, Azure Blob, Brevo email |
| `src/backend/Shora.Contracts`      | API DTO records consumed by Api and mirrored in TS                |
| `src/frontend`                     | Angular SPA; `@contracts/*` alias points to `src/contracts`       |
| `specs/`                           | Maintainer-facing design documentation                            |
| `docs/`                            | Deployment and ops documentation                                  |

---

## 5. Architecture

Production runs as **one HTTPS origin**: the browser loads the Angular app and calls `/api/v1/*` on the same host. Refresh-token cookies use `SameSite=Strict`, which requires same-site deployment in MVP.

The Render container runs **Shora.Api** (HTTP + background jobs) and serves the Angular SPA from **wwwroot**. The API connects to **Neon PostgreSQL**, **Azure Blob Storage** (receipts), and **Brevo** for email.

**Dependency direction (backend):**

- `Shora.Api` → `Shora.Application`, `Shora.Infrastructure`
- `Shora.Infrastructure` → `Shora.Application`, `Shora.Domain`
- `Shora.Application` → `Shora.Domain`
- `Shora.Domain` → (nothing)
- `Shora.Contracts` → (nothing; referenced by Api + Application)

---

## 6. Backend architecture

### Layers

| Layer              | Responsibility                                                               |
| ------------------ | ---------------------------------------------------------------------------- |
| **Domain**         | Entities, enums, invariants                                                  |
| **Application**    | Services, validators, `Result` pattern, `IApplicationDbContext`, outbox, ops |
| **Infrastructure** | EF Core, Identity, JWT, refresh tokens, Brevo email, Azure Blob, seeder      |
| **Api**            | Controllers, middleware, rate limits, background job hosts                   |
| **Contracts**      | Shared DTO records (no business logic)                                       |

There are **no per-entity repository classes** — Application uses `IApplicationDbContext` (implemented by `ApplicationDbContext` in Infrastructure).

### Request pipeline

1. `CorrelationIdMiddleware` — sets `X-Correlation-Id`
2. `GlobalExceptionHandler` — unhandled exceptions → Problem Details 500
3. HSTS (non-Development)
4. HTTPS redirection
5. CORS (`SpaCors` policy)
6. Static files + SPA fallback (non-Development only)
7. Authentication (JWT Bearer)
8. Authorization (roles)
9. Rate limiter
10. Output cache
11. Controller → Application service → EF Core / `IFileStorage` / `IEmailSender`
12. `Result` → Problem Details or 200/201 JSON

### Key middleware and cross-cutting

- **Validation:** FluentValidation-style validators in Application; model binding errors map to `ValidationProblemDetails`
- **Errors:** RFC 7807 Problem Details with extension field `code` and type URI `https://shora.dev/errors/{code}`
- **Background work:** `BackgroundService` hosts in `Shora.Api/BackgroundJobs/` (not Hangfire/Quartz)
- **Startup:** `InitializeDatabaseAsync()` runs EF migrations + idempotent seed on every boot

---

## 7. Frontend architecture

| Item                   | Detail                                                                          |
| ---------------------- | ------------------------------------------------------------------------------- |
| **Framework**          | Angular 21, standalone components, no NgModules                                 |
| **Bootstrap**          | `bootstrapApplication(App, appConfig)`                                          |
| **Routing**            | Lazy-loaded feature routes under `ShellComponent` (header/footer)               |
| **State**              | Signals + services; booking flow uses `sessionStorage`                          |
| **HTTP**               | `auth.interceptor.ts` — Bearer token + `withCredentials: true`                  |
| **Auth token storage** | Access JWT **in memory only** (lost on full page reload until refresh succeeds) |
| **API base URL**       | `environment.apiBaseUrl` = `/api/v1` (relative — same origin in prod)           |
| **Dev proxy**          | `proxy.conf.json` → `https://localhost:7183`                                    |
| **i18n**               | No translation library; UI strings hardcoded in templates                       |
| **RTL**                | `<html lang="ar" dir="rtl">`; LTR overrides on phone/payment fields             |

### Routes

| Area    | Paths                                                                                                  |
| ------- | ------------------------------------------------------------------------------------------------------ |
| Public  | `/`, `/about`, `/services`, `/privacy`, `/terms`                                                       |
| Auth    | `/auth/login`, `/auth/signup`, `/auth/verify-email`, `/auth/forgot-password`, `/auth/reset-password`   |
| Booking | `/booking/start` → `/booking/delivery` → `/booking/phone` → `/booking/review` → `/booking/payment/:id` |
| Client  | `/dashboard`                                                                                           |
| Admin   | `/admin/settings`, `/admin/availability`, `/admin/bookings`, `/admin/earnings`, `/admin/ops`           |

### Guards

| Guard                           | Behavior                                        |
| ------------------------------- | ----------------------------------------------- |
| `clientGuard`                   | Requires auth; admins redirected to `/admin`    |
| `adminGuard`                    | Requires `Admin` role                           |
| `bookingSlotSelectedGuard` etc. | Enforce booking flow order via `sessionStorage` |

More detail: [`src/frontend/README.md`](src/frontend/README.md).

---

## 8. Authentication & authorization

### Registration

- `POST /api/v1/auth/signup` — creates `Client` user, returns JWT + sets refresh cookie
- Email verification required **before booking** (not before login)
- Google users are auto-verified

### Login / logout

- `POST /api/v1/auth/login` — JWT in body, refresh in httpOnly cookie
- `POST /api/v1/auth/logout` — revokes refresh token; requires `[ValidateAuthCookieOrigin]`
- `POST /api/v1/auth/refresh` — rotates refresh token; 60s grace window for multi-tab races; reuse after grace revokes all user tokens

### Tokens

| Token            | Storage                         | Lifetime                                  | Notes                           |
| ---------------- | ------------------------------- | ----------------------------------------- | ------------------------------- |
| **Access (JWT)** | Frontend memory                 | 15 min default (`Jwt:AccessTokenMinutes`) | Sent as `Authorization: Bearer` |
| **Refresh**      | httpOnly cookie `shora_refresh` | 7 days default (`Jwt:RefreshTokenDays`)   | Path `/api`; not readable by JS |

### Refresh cookie flags

| Environment | Secure  | SameSite |
| ----------- | ------- | -------- |
| Development | `false` | `Strict` |
| Production  | `true`  | `Strict` |

Cookie name and path are defined in code (`RefreshCookieOptions`), not in `appsettings`.

### Google Sign-In

- `POST /api/v1/auth/google` with `{ idToken }`
- Server validates ID token against `Google:ClientId` only
- `Google:ClientSecret` is in config but **not used** by the server (ID-token flow)
- Frontend `googleClientId` in `environment.production.ts` must be set at build time; empty = button hidden

### Roles

| Role     | Access                                    |
| -------- | ----------------------------------------- |
| `Client` | Booking, dashboard, own bookings/payments |
| `Admin`  | All `/api/v1/admin/*` endpoints           |

No public admin registration. First admin via `AdminSeed` env vars or dev `appsettings.Development.json`.

### CORS / CSRF considerations

- MVP requires **same registrable domain** for SPA and API
- CORS policy allows configured origins with credentials
- Refresh/logout validate `Origin` or `Referer` against `Cors:AllowedOrigins`
- No separate CSRF token — mitigated by same-site + Strict cookies + origin checks on cookie endpoints

### Auth flow (session restore)

On app boot, Angular calls `POST /auth/refresh` with the httpOnly cookie. On success, the access token is stored in memory. Subsequent API calls send `Authorization: Bearer` plus credentials. On **401**, the interceptor calls refresh once and retries with an `X-Retry` header; if refresh fails, the user is sent to login.

---

## 9. Database

| Item           | Value                                           |
| -------------- | ----------------------------------------------- |
| **Engine**     | PostgreSQL (Npgsql)                             |
| **ORM**        | EF Core 10                                      |
| **Context**    | `ApplicationDbContext` (`Shora.Infrastructure`) |
| **Migrations** | `Shora.Infrastructure/Migrations/`              |
| **Startup**    | `MigrateAsync()` on every app start             |
| **Tests**      | Ephemeral DB per test via Testcontainers        |

> **Note:** Base `appsettings.json` still contains a **LocalDB placeholder** connection string. Runtime uses PostgreSQL — configure via `appsettings.Development.json` or user-secrets.

### Entity relationships

- `ApplicationUser` has many `Booking` records; each `Booking` has one `Payment` and optionally one `CancellationRequest`
- `Payment` has many `PaymentReceipt` uploads over time
- `Booking` reserves one `AvailabilitySlot`; slots are generated from `AvailabilityWindow` templates
- `Settings` is a singleton row; `OutboxMessage` and `RefreshToken` are supporting tables
- `BookingStatusAudit` records status transitions per booking

See **Important entities** below for field-level detail.

### Important entities

| Entity                | Role                                                             |
| --------------------- | ---------------------------------------------------------------- |
| `ApplicationUser`     | Identity user (`Client` or `Admin`)                              |
| `Settings`            | Singleton (`Id = 1`) — price, duration, payment numbers, windows |
| `AvailabilityWindow`  | Recurring weekly availability template                           |
| `AvailabilitySlot`    | Concrete UTC slot; `IsBooked` flag                               |
| `BlockedDate`         | Consultant unavailable range                                     |
| `Booking`             | Session reservation with status lifecycle                        |
| `Payment`             | 1:1 with booking; amount in EGP                                  |
| `PaymentReceipt`      | Uploaded proof image metadata + review state                     |
| `CancellationRequest` | Client-initiated cancel with admin decision                      |
| `OutboxMessage`       | Transactional email queue                                        |
| `RefreshToken`        | Hashed refresh token store                                       |
| `JobRunHistory`       | Background job heartbeat                                         |

### Indexes and constraints

Configured in EF entity configurations (Infrastructure). Notable rules:

- Admin FK delete behavior uses `ON DELETE NO ACTION` where PostgreSQL cascade paths would conflict
- Booking uses optimistic concurrency (`RowVersion` / xmin)

---

## 10. Domain model & business rules

### Booking statuses

| Status                  | Meaning                                               |
| ----------------------- | ----------------------------------------------------- |
| `PendingPayment`        | Slot held; client must upload receipt before deadline |
| `PendingApproval`       | Receipt uploaded; awaiting admin review               |
| `Confirmed`             | Admin approved payment                                |
| `CancellationRequested` | Client asked to cancel; admin must decide             |
| `Completed`             | Session end time passed (automatic job)               |
| `Cancelled`             | Hold released or session cancelled                    |

### Payment statuses

| Status            | Meaning                |
| ----------------- | ---------------------- |
| `AwaitingReceipt` | No valid receipt yet   |
| `UnderReview`     | Receipt submitted      |
| `Approved`        | Admin approved         |
| `Refunded`        | Manual refund recorded |
| `Void`            | Terminal void state    |

### Key business rules

| Rule                            | Detail                                                                                                     |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| **Email verified to book**      | Unverified users can log in but `POST /bookings` rejects them                                              |
| **Login at reserve step**       | Public can browse slots; account required only when reserving                                              |
| **Hold cap**                    | Max **3** concurrent `PendingPayment`/`PendingApproval` bookings per client (`Booking:UnconfirmedHoldCap`) |
| **Upload deadline**             | Configurable via `Settings.ReceiptUploadWindowMinutes` (default 60 min)                                    |
| **Missed deadline**             | Background job cancels hold and releases slot                                                              |
| **Flat pricing**                | Single session price from `Settings` (default 500 EGP)                                                     |
| **No online gateway**           | Payment is manual transfer + receipt upload only                                                           |
| **Confirmation**                | Booking becomes `Confirmed` only after **admin approves** receipt                                          |
| **Cancellation window**         | Client can request cancel until `SlotStart - CancellationRequestAutoDeclineHours` (default 1h)             |
| **Auto-decline cancel request** | Job declines pending requests at `AutoDeclineAtUtc`                                                        |
| **No self-service reschedule**  | Client must cancel and rebook                                                                              |
| **Delivery**                    | Voice call (requires phone) or chat (WhatsApp link at session time)                                        |
| **Privacy**                     | Clients may use pseudonym display names                                                                    |
| **Admin-only refunds**          | Manual record only; no automated payout integration                                                        |

---

## 11. Important workflows

### Booking + payment (happy path)

1. Client selects slot and delivery method; SPA calls `POST /bookings` (auth required)
2. API creates `PendingPayment` booking and holds the slot
3. Client uploads receipt via `POST /payments/{bookingId}/receipt`; blob stored, status → `PendingApproval`; outbox notifies admin
4. Admin approves receipt → `Confirmed` + `Payment.Approved`; confirmation emails queued

### Failure cases (booking/payment)

| Scenario               | System behavior                                                             |
| ---------------------- | --------------------------------------------------------------------------- |
| Upload deadline passes | Job cancels booking, releases slot, sends cancellation email                |
| Admin declines receipt | Booking returns to `PendingPayment` with new deadline; client may re-upload |
| Client cancels hold    | Slot released immediately; counts toward hold cap freed                     |
| Hold cap exceeded      | Reserve rejected with error                                                 |
| Unverified email       | Reserve rejected                                                            |

### Auth email vs transactional email

- **Auth** (verify, reset): sent immediately via `AuthEmailService`
- **Booking/payment/cancel/refund**: written to `OutboxMessage` in same DB transaction, dispatched by background job

---

## 12. Payments

Shora does **not** integrate with Stripe, PayPal, or other payment providers.

| Aspect              | Implementation                                                  |
| ------------------- | --------------------------------------------------------------- |
| **Payment model**   | Manual Vodafone Cash or InstaPay transfer                       |
| **Proof**           | Client uploads JPEG/PNG receipt (max 5 MB)                      |
| **Verification**    | Admin human review                                              |
| **Source of truth** | `Payment` + `PaymentReceipt` entities in PostgreSQL             |
| **Approval**        | `POST /admin/bookings/{id}/receipts/approve` → `Confirmed`      |
| **Decline**         | Typed reason code + optional note → fresh upload window         |
| **Refunds**         | Admin `POST /admin/payments/{id}/refunds/record` with reference |
| **Idempotency**     | Content hash duplicate detection (warning, not hard block)      |
| **Webhooks**        | None — no external payment callbacks                            |

---

## 13. File storage

| Item             | Detail                                                                                                   |
| ---------------- | -------------------------------------------------------------------------------------------------------- |
| **Provider**     | Azure Blob Storage (`IFileStorage` → `AzureBlobFileStorage`)                                             |
| **Dev**          | Azurite via `Storage:ConnectionString=UseDevelopmentStorage=true`                                        |
| **Container**    | Private `Storage:ReceiptContainer` (default `receipts`)                                                  |
| **Upload flow**  | Multipart → validate type/size → `temp/{guid}` → DB row → finalize to `receipts/{paymentId}/{receiptId}` |
| **Admin read**   | Short-lived SAS URLs (`Storage:ReceiptReadUrlMinutes`, default 5) only when malware scan = `Clean`       |
| **Malware scan** | `PassThroughMalwareScanner` — always marks `Clean` (no external AV)                                      |
| **Cleanup**      | Jobs delete orphan temp blobs and purge old receipts per retention settings                              |

If `Storage:ConnectionString` is unset, `NotImplementedFileStorage` throws on upload.

---

## 14. Email / notifications

| Type              | Mechanism                              | Templates                                             |
| ----------------- | -------------------------------------- | ----------------------------------------------------- |
| **Auth emails**   | Direct Brevo via `AuthEmailService`    | Embedded HTML in `Shora.Application/Email/Templates/` |
| **Transactional** | Outbox pattern + `OutboxDispatcherJob` | Rendered by `OutboxEmailRenderer`                     |

### Outbox message types

- Booking confirmed (client + admin)
- Receipt uploaded (admin)
- Receipt declined (client)
- Booking cancelled
- Cancellation request (admin) / declined (client)
- Refund confirmation / refund revocation

### Retry policy

Max **8** attempts with escalating backoff; then `DeadLettered`. Dev uses log sender.

### When emails fail

- Auth: operation may succeed but user won't receive link (logged)
- Outbox: retried by job; ops alerts on dead-letter burst

---

## 15. Background jobs

All gated by `BackgroundJobs:Enabled` (default `true`). Disabled in tests.

| Job                                 | Default interval | Purpose                                  |
| ----------------------------------- | ---------------- | ---------------------------------------- |
| `ReceiptUploadDeadlineCleanupJob`   | 60s              | Cancel expired `PendingPayment` holds    |
| `OutboxDispatcherJob`               | 60s              | Send pending outbox emails (batch 20)    |
| `CancellationRequestAutoDeclineJob` | 60s              | Auto-decline stale cancellation requests |
| `BookingAutoCompleteJob`            | 300s             | Mark past sessions `Completed`           |
| `RefreshTokenPurgeJob`              | 86400s           | Delete expired refresh tokens            |
| `ReceiptBlobReconciliationJob`      | 900s             | Repair stuck blob finalization           |
| `ReceiptRetentionPurgeJob`          | 86400s           | Delete old receipt blobs                 |
| `TempBlobCleanupJob`                | 86400s           | Delete old `temp/` blobs                 |
| `AvailabilityTopUpJob`              | 86400s           | Regenerate slot horizon                  |
| `OpsMonitoringJob`                  | 300s             | Evaluate ops alerts (120s initial delay) |

Heartbeats recorded in `JobRunHistory` for ops monitoring.

---

## 16. API

- **Base path:** `/api/v1/`
- **Versioning:** ASP.NET API versioning (`[ApiVersion("1.0")]`)
- **OpenAPI (Development only):** `/openapi/v1.json`
- **Health:** `GET /api/v1/health` → `{ status, timestampUtc }` (503 if PostgreSQL unhealthy)
- **Errors:** RFC 7807 Problem Details with `code` extension

Full conventions: [`specs/00-api-conventions.md`](specs/00-api-conventions.md).

### Endpoint summary

#### Public

| Method | Endpoint                                          | Auth   | Purpose                           |
| ------ | ------------------------------------------------- | ------ | --------------------------------- |
| GET    | `/health`                                         | —      | Health check                      |
| GET    | `/availability?from=&to=`                         | —      | Available slots (rate limited)    |
| GET    | `/settings/public`                                | —      | Session price & duration (cached) |
| POST   | `/auth/signup`, `/auth/login`, `/auth/google`     | —      | Register / login                  |
| POST   | `/auth/refresh`, `/auth/logout`                   | —      | Session refresh / logout          |
| POST   | `/auth/verify-email`, `/auth/resend-verification` | —      | Email verification                |
| POST   | `/auth/forgot-password`, `/auth/reset-password`   | —      | Password reset                    |
| GET    | `/auth/me`                                        | Bearer | Current user                      |

#### Client (`Client` role)

| Method | Endpoint                               | Purpose                            |
| ------ | -------------------------------------- | ---------------------------------- |
| POST   | `/bookings`                            | Reserve slot                       |
| GET    | `/bookings/mine`                       | List own bookings (paginated past) |
| POST   | `/bookings/{id}/cancel`                | Cancel hold                        |
| GET    | `/bookings/{id}/payment-instructions`  | Payment details                    |
| POST   | `/bookings/{id}/cancellation-requests` | Request cancellation               |
| POST   | `/payments/{bookingId}/receipt`        | Upload receipt (multipart)         |

#### Admin (`Admin` role)

| Method          | Endpoint                                                      | Purpose                |
| --------------- | ------------------------------------------------------------- | ---------------------- |
| GET/PUT         | `/admin/settings`                                             | Consultant settings    |
| CRUD            | `/admin/availability-windows`                                 | Recurring windows      |
| GET/POST/DELETE | `/admin/blocked-dates`                                        | Blocked ranges         |
| GET             | `/admin/bookings`                                             | Booking list           |
| POST            | `/admin/bookings/{id}/receipts/approve\|decline`              | Receipt review         |
| POST            | `/admin/bookings/{id}/cancel`                                 | Direct cancel          |
| POST            | `/admin/bookings/{id}/cancellation-requests/approve\|decline` | Cancellation decisions |
| POST            | `/admin/payments/{id}/refunds/record`                         | Manual refunds         |
| GET             | `/admin/earnings?from=&to=`                                   | Revenue summary        |
| GET             | `/admin/ops/alerts`, `/admin/ops/runbooks`                    | Ops monitoring         |

---

## 17. Configuration

Use `__` (double underscore) for nested env vars on Render (e.g. `Jwt__SigningKey`).

### Required in Production (`ValidateOnStart`)

| Variable / setting                     | Purpose                              | Example                                  |
| -------------------------------------- | ------------------------------------ | ---------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`               | Environment                          | `Production`                             |
| `ConnectionStrings__DefaultConnection` | Neon PostgreSQL                      | `Host=...;Database=...;Ssl Mode=Require` |
| `Jwt__SigningKey`                      | HMAC signing key (≥32 chars)         | `YOUR_RANDOM_SECRET_32_CHARS_MIN`        |
| `Frontend__BaseUrl`                    | Public HTTPS URL (no trailing slash) | `https://shora.onrender.com`             |
| `Cors__AllowedOrigins__0`              | Must match `Frontend__BaseUrl`       | Same as above                            |
| `AllowedHosts`                         | Hostname only                        | `shora.onrender.com`                     |
| `Storage__ConnectionString`            | Azure Blob                           | From Azure Portal / CLI                  |
| `Storage__ReceiptContainer`            | Private container                    | `receipts`                               |
| `Email__ApiKey`                        | Brevo API key                        | `xkeysib-...`                            |
| `Email__FromAddress`                   | Verified sender                      | `noreply@yourdomain.com`                 |

### Required for full functionality (not always validated at startup)

| Variable / setting                         | Purpose                                   |
| ------------------------------------------ | ----------------------------------------- |
| `AdminSeed__Email` / `AdminSeed__Password` | One-time first admin (remove after login) |

### Optional

| Variable / setting              | Purpose                                       | Default                 |
| ------------------------------- | --------------------------------------------- | ----------------------- |
| `Google__ClientId`              | Google Sign-In                                | Empty = disabled        |
| `Google__ClientSecret`          | **Unused by server**                          | —                       |
| `Seed__*`                       | Payment/contact defaults before first startup | Placeholder test values |
| `Jwt__AccessTokenMinutes`       | Access token TTL                              | 15                      |
| `Jwt__RefreshTokenDays`         | Refresh token TTL                             | 7                       |
| `BackgroundJobs__Enabled`       | Toggle jobs                                   | `true`                  |
| `Cache__Enabled`                | In-memory cache                               | `true`                  |
| `Booking__UnconfirmedHoldCap`   | Max pending holds                             | 3                       |
| `ReceiptUpload__MaxSizeBytes`   | Max receipt size                              | 5242880 (5 MB)          |
| Rate limiting `RateLimiting__*` | Per-endpoint limits                           | See `appsettings.json`  |

### Development-only

| Setting               | Location                                              | Notes                                         |
| --------------------- | ----------------------------------------------------- | --------------------------------------------- |
| PostgreSQL connection | `appsettings.Development.json` or user-secrets        | Replace `YOUR_PG_USER` / `YOUR_PG_PASSWORD`   |
| `AdminSeed`           | `appsettings.Development.json`                        | Default `admin@localhost.dev`                 |
| `Jwt:SigningKey`      | `appsettings.Development.json`                        | Dev-only key included                         |
| Azurite               | `Storage:ConnectionString=UseDevelopmentStorage=true` | Requires Azurite on port 10000                |
| Email                 | Unconfigured                                          | Logged to console via `DevLoggingEmailSender` |

### Frontend (build-time)

| File                                                      | Setting                 | Notes                                             |
| --------------------------------------------------------- | ----------------------- | ------------------------------------------------- |
| `src/frontend/src/environments/environment.production.ts` | `googleClientId`        | Commit before merge to `main` for production builds |
| Both env files                                            | `apiBaseUrl: '/api/v1'` | Same-origin deploy                                |

Full production reference: [`docs/deployment.md`](docs/deployment.md).

---

## 18. Local development setup

### Prerequisites

| Tool           | Version                        | Required for                   |
| -------------- | ------------------------------ | ------------------------------ |
| .NET SDK       | **10.0.x**                     | Backend build/run/test         |
| Node.js        | **22.x**                       | Frontend (Angular 21)          |
| npm            | **10.x** (project uses 10.9.4) | Frontend dependencies          |
| PostgreSQL     | **16+**                        | Local database                 |
| Docker Desktop | Latest                         | Backend tests (Testcontainers) |
| Azurite        | Docker image or local          | Receipt upload in dev          |

Angular CLI is invoked via `npx ng` / `npm run ng` (local devDependency).

### 1. Clone and install

```powershell
git clone https://github.com/MohamedMamdoouh/shora-consulting-platform.git
cd shora-consulting-platform

cd src/frontend
npm install

cd ../backend
dotnet restore
```

### 2. PostgreSQL

```sql
CREATE DATABASE "Shora";
```

Configure credentials (user-secrets recommended):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=Shora;Username=YOUR_PG_USER;Password=YOUR_PG_PASSWORD" --project src/backend/Shora.Api
```

Or edit [`src/backend/Shora.Api/appsettings.Development.json`](src/backend/Shora.Api/appsettings.Development.json).

### 3. Azurite (receipt uploads)

```powershell
docker run --rm -p 10000:10000 mcr.microsoft.com/azure-storage/azurite:3.35.0 azurite-blob --blobHost 0.0.0.0 --blobPort 10000 --skipApiVersionCheck
```

### 4. Database migrations

Migrations run automatically on API startup. To apply manually:

```powershell
cd src/backend
dotnet ef database update --project Shora.Infrastructure --startup-project Shora.Api
```

### 5. Run backend

```powershell
cd src/backend
dotnet run --project Shora.Api
```

| URL         | Value                                    |
| ----------- | ---------------------------------------- |
| API (HTTPS) | `https://localhost:7183`                 |
| OpenAPI     | `https://localhost:7183/openapi/v1.json` |
| Health      | `https://localhost:7183/api/v1/health`   |

### 6. Run frontend

```powershell
cd src/frontend
npm start
```

| URL | Value                   |
| --- | ----------------------- |
| SPA | `http://localhost:4200` |

**Important:** Use `http://localhost:4200` in the browser for auth testing — the dev proxy makes API calls same-origin so refresh cookies work.

Default dev admin (from `appsettings.Development.json`): `admin@localhost.dev` / `ChangeMe123!`

---

## 19. Development workflow

1. Create a feature branch from `main`
2. Run backend + frontend locally (see above)
3. Change C# DTO in `Shora.Contracts` → mirror in `src/contracts/`
4. Add EF migration if entities changed:

```powershell
cd src/backend
dotnet ef migrations add YourMigrationName --project Shora.Infrastructure --startup-project Shora.Api
```

5. Run tests before pushing (see [Testing](#20-testing))
6. Open PR to `main` — CI runs path-filtered backend/frontend jobs
7. Merge to `main` triggers Render auto-deploy (Docker build from Git)

**Formatting:** Prettier is a frontend devDependency; no repo-wide dotnet format config documented.

**Debugging:** Stop running `Shora.Api` before `dotnet build` — locked DLLs break the build.

---

## 20. Testing

| Item                  | Detail                                                                 |
| --------------------- | ---------------------------------------------------------------------- |
| **Framework**         | xUnit v3                                                               |
| **Project**           | `src/backend/Shora.Tests`                                              |
| **Unit tests**        | `Unit/` — validators, mappers, retry logic                             |
| **Integration tests** | `Integration/Api/`, `Integration/Infrastructure/`, `Integration/Auth/` |
| **Test DB**           | Testcontainers PostgreSQL — **Docker must be running**                 |
| **Blob tests**        | Testcontainers Azurite or `InMemoryFileStorage`                        |
| **Frontend tests**    | Vitest via `ng test`                                                   |

### Commands

```powershell
# Backend (requires Docker)
cd src/backend
dotnet build
dotnet test

# Frontend
cd src/frontend
npm ci
npm run build
$env:CI = "true"; npm test
```

Tests set `BackgroundJobs:Enabled = false` where needed to avoid background interference.

---

## 21. Docker

| Item                   | Detail                                                                |
| ---------------------- | --------------------------------------------------------------------- |
| **Dockerfile**         | Single-stage runtime image; copies pre-built `./publish`              |
| **Base image**         | `mcr.microsoft.com/dotnet/aspnet:10.0`                                |
| **Port**               | `8080` (`ASPNETCORE_HTTP_PORTS=8080` on Render)                        |
| **docker-compose**     | **Not present** in repository                                         |
| **Local Docker usage** | Azurite for dev; Testcontainers for tests; full app image built in CI |

Build locally (after manual publish):

```powershell
# Build publish folder first (see deploy workflow steps)
docker build -t shora:local .
docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="..." shora:local
```

---

## 22. CI/CD

### CI (`ci.yml`)

| Trigger | `push` and `pull_request` to `main` |
| Job | Runs when |
| --- | --- |
| **Detect changes** | Always — `dorny/paths-filter@v3` |
| **Backend** | `src/backend/**` or `.github/workflows/**` changed |
| **Frontend** | `src/frontend/**` or workflows changed |

No secrets required. Docs-only changes skip unaffected jobs.

Production deploys: push/merge to `main` → Render builds [`Dockerfile`](Dockerfile) and deploys automatically. See [`docs/deployment.md`](docs/deployment.md).

Optional repository variable: `PRODUCTION_URL` (e.g. `https://shora.onrender.com`) for documentation/scripts.

Detail: [`.github/workflows/README.md`](.github/workflows/README.md).

---

## 23. Deployment

| Component       | Provider                       |
| --------------- | ------------------------------ |
| Compute + SPA   | Render (Docker build from Git) |
| Database        | Neon PostgreSQL                |
| Receipt storage | Azure Blob (private container) |
| Email           | Brevo (HTTPS API)              |

- Frontend is baked into `Shora.Api/wwwroot` during the Docker build on Render
- EF migrations run on container startup (forward-only — no auto rollback)
- Health check: `/api/v1/health`

Step-by-step guide: [`docs/deployment.md`](docs/deployment.md).

---

## 24. Security

| Mechanism            | Implementation                                           |
| -------------------- | -------------------------------------------------------- |
| **Passwords**        | ASP.NET Identity hashing (8+ chars, digit, upper, lower) |
| **JWT**              | HMAC-SHA256; validated issuer/audience/lifetime          |
| **Refresh tokens**   | Opaque, SHA-256 hashed at rest, rotation on refresh      |
| **Cookies**          | httpOnly, SameSite=Strict, Secure in production          |
| **CORS**             | Explicit allowed origins with credentials                |
| **Rate limiting**    | Per-IP partitions; 429 with `Retry-After`                |
| **Input validation** | FluentValidation + model binding                         |
| **File upload**      | Size (5 MB), MIME validation, content hash anti-replay   |
| **SQL injection**    | EF Core parameterized queries                            |
| **Secrets**          | Env vars / user-secrets — not committed                  |
| **HSTS**             | Enabled non-Development                                  |

### Limitations (honest)

- No real malware scanning on receipts (`PassThroughMalwareScanner`)
- No CSRF tokens (relies on same-site cookies + origin checks)
- No horizontal scaling / distributed cache in MVP
- Manual refunds with no automated payout verification
- `Google:ClientSecret` configured but unused

---

## 25. Error handling & observability

| Item               | Detail                                                         |
| ------------------ | -------------------------------------------------------------- |
| **API errors**     | RFC 7807 Problem Details; `code` extension for app error codes |
| **Validation**     | 400 `ValidationProblemDetails`                                 |
| **Unhandled**      | 500; exception message in Development only                     |
| **Correlation ID** | `X-Correlation-Id` middleware; logged on failures              |
| **Logging**        | Standard ASP.NET Core logging (`Logging:LogLevel`)             |
| **Health**         | `GET /api/v1/health` includes PostgreSQL check                 |
| **Ops alerts**     | `OpsMonitoringService` + admin UI at `/admin/ops`              |
| **APM**            | **Not integrated** in MVP                                      |

When something fails: check app logs, correlation ID, `/admin/ops` alerts, and outbox dead-letter state.

---

## 26. Timezones & date/time handling

| Rule                     | Detail                                               |
| ------------------------ | ---------------------------------------------------- |
| **Storage**              | UTC in database (`SlotStartUtc`, `SlotEndUtc`, etc.) |
| **Availability windows** | Stored as UTC day/time templates                     |
| **Client display**       | Frontend converts UTC to browser local timezone      |
| **Admin filters**        | Date filters on booking slot times (UTC semantics)   |
| **Auto-complete**        | Job compares `SlotEndUtc` to UTC now                 |

Always persist and compare business times in **UTC** on the server.

---

## 27. Important design decisions

| Decision                           | Rationale (from code/docs)                             | Trade-off                                                                  |
| ---------------------------------- | ------------------------------------------------------ | -------------------------------------------------------------------------- |
| **Same-site SPA + API**            | Required for `SameSite=Strict` refresh cookies         | No cross-subdomain API in MVP                                              |
| **Manual payments**                | No payment gateway fees/complexity for local transfers | Admin must review every receipt                                            |
| **Outbox for transactional email** | Consistent with DB transactions                        | Delayed delivery (job interval)                                            |
| **No repository layer**            | EF `DbContext` is the unit of work                     | Less abstraction, simpler codebase                                         |
| **In-process background jobs**     | Single Render container, no worker service             | No horizontal scaling of jobs                                              |
| **Access token in memory**         | Reduces XSS token theft surface                        | Full page reload requires refresh call                                     |
| **Manual TS contracts**            | No codegen for MVP                                     | Must keep C#/TS in sync manually                                           |
| **Startup migrations**             | Simpler deploy pipeline                                | Bad migrations affect production immediately; rollback is forward-fix only |

---

## 28. Common pitfalls

| Pitfall                                                 | Why it breaks things                                            |
| ------------------------------------------------------- | --------------------------------------------------------------- |
| Using `https://localhost:7183` directly in browser      | Refresh cookies won't work cross-origin with Angular dev server |
| Missing Azurite                                         | Receipt upload throws `NotImplementedException`                 |
| Docker not running                                      | `dotnet test` fails (Testcontainers)                            |
| `Cors__AllowedOrigins__0` ≠ `Frontend__BaseUrl` in prod | Startup validation fails                                        |
| Neon URI connection string on Render                    | Values with `=` may truncate                                    |
| Empty `googleClientId`                                  | Google button hidden (expected)                                 |
| Forgetting to remove `AdminSeed__*`                     | Admin credentials remain in env                                 |
| Running API during `dotnet build`                       | DLL lock errors                                                 |
| Base `appsettings.json` LocalDB string                  | Misleading — use Development config for PostgreSQL              |

---

## 29. Troubleshooting

### Backend cannot connect to database

**Cause:** Wrong or missing `ConnectionStrings:DefaultConnection`.

**Solution:** Set PostgreSQL connection in user-secrets or `appsettings.Development.json`. Verify database exists.

### Auth works on API URL but not localhost:4200

**Cause:** Bypassing Angular dev proxy.

**Solution:** Always use `http://localhost:4200`; ensure proxy targets `https://localhost:7183`.

### Receipt upload fails in dev

**Cause:** Azurite not running or wrong `Storage:ConnectionString`.

**Solution:** Start Azurite on port 10000; confirm `UseDevelopmentStorage=true` in Development config.

### CORS validation error on Render startup

**Cause:** Render vars still set to `http://localhost:4200`.

**Solution:** Set `Frontend__BaseUrl` and `Cors__AllowedOrigins__0` to production HTTPS URL.

### Render Docker build fails

**Cause:** Frontend or backend compile error, or missing env vars at runtime.

**Solution:** Check Render build/deploy logs. Reproduce locally with `docker build -t shora:local .` See [`docs/deployment.md`](docs/deployment.md).

### `dotnet test` hangs or fails immediately

**Cause:** Docker Desktop not running.

**Solution:** Start Docker; re-run tests.

---

## 30. Useful commands

```powershell
# Backend
cd src/backend
dotnet restore
dotnet build
dotnet run --project Shora.Api
dotnet test
dotnet ef migrations add Name --project Shora.Infrastructure --startup-project Shora.Api
dotnet ef database update --project Shora.Infrastructure --startup-project Shora.Api

# User secrets (example)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;..." --project Shora.Api

# Frontend
cd src/frontend
npm install
npm start
npm run build
npm test

# Azurite
docker run --rm -p 10000:10000 mcr.microsoft.com/azure-storage/azurite:3.35.0 azurite-blob --blobHost 0.0.0.0 --blobPort 10000 --skipApiVersionCheck
```

---

## 31. Project conventions

| Area               | Convention                                                        |
| ------------------ | ----------------------------------------------------------------- |
| **API routes**     | `/api/v1/...`; versioning via ASP.NET attribute                   |
| **Errors**         | `Result` pattern in Application; Problem Details in Api           |
| **DTOs**           | C# records in `Shora.Contracts`; TS interfaces in `src/contracts` |
| **JSON**           | camelCase serialization                                           |
| **Entities**       | Plain classes in Domain; EF config in Infrastructure              |
| **Validation**     | Application-layer validators                                      |
| **NuGet versions** | Central package management (`Directory.Packages.props`)           |
| **Branching**      | `main` protected; CI on PR                                        |
| **Commits**        | No enforced convention documented in repo                         |

API conventions detail: [`specs/00-api-conventions.md`](specs/00-api-conventions.md).  
Contract sync: [`src/contracts/README.md`](src/contracts/README.md).

---

## 32. Glossary

| Term                 | Meaning                                                                            |
| -------------------- | ---------------------------------------------------------------------------------- |
| **Hold**             | Reserved slot in `PendingPayment` before receipt approval                          |
| **Receipt**          | Image proof of manual bank transfer                                                |
| **Outbox**           | DB queue for reliable transactional email                                          |
| **Slot**             | Concrete UTC appointment time generated from availability windows                  |
| **Delivery method**  | Voice call or chat (WhatsApp) for the session                                      |
| **AdminSeed**        | One-time env-based admin account bootstrap                                         |
| **SAS URL**          | Time-limited Azure Blob read URL for admin receipt viewing                         |
| **Same-site deploy** | SPA and API served from one HTTPS origin                                           |
| **Ops alert**        | Automated warning from background monitoring (stale jobs, pending approvals, etc.) |

---

## 33. Future improvements / known limitations

Documented in code/specs (not a committed roadmap):

| Limitation                                           | Source                                              |
| ---------------------------------------------------- | --------------------------------------------------- |
| Placeholder practitioner bio/copy on public pages    | `specs/03-public-pages.md` open items               |
| No real malware scanner on receipts                  | `PassThroughMalwareScanner`                         |
| No online payment gateway                            | By design (`specs/05-payments.md`)                  |
| No horizontal scaling / Redis                        | MVP topology (`specs/08-cross-cutting-concerns.md`) |
| `Google:ClientSecret` unused                         | Config present; ID-token flow only                  |
| Base `appsettings.json` LocalDB placeholder          | Misleading default; PostgreSQL is actual engine     |
| No automated post-deploy smoke tests | Deploy is Render auto-deploy on `main`               |
| SEO files (`robots.txt`, `sitemap.xml`) not shipped  | Documented in deployment guide                      |

---

## Further reading

| Document                                                     | Audience                        |
| ------------------------------------------------------------ | ------------------------------- |
| [`src/frontend/README.md`](src/frontend/README.md)           | Frontend routes and features    |
| [`src/contracts/README.md`](src/contracts/README.md)         | Keeping TS/C# contracts aligned |
| [`docs/deployment.md`](docs/deployment.md)                   | Production hosting              |
| [`specs/`](specs/)                                           | Maintainer design reference     |
| [`.github/workflows/README.md`](.github/workflows/README.md) | CI/CD workflows                 |

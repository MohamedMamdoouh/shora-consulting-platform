# 00 — API Conventions (Cross-Cutting)

Status: **Implemented** — applies to all specs (01–09) and every new endpoint.

## 1. Result pattern (Application layer)

- Application services return `Result` / `Result<T>` from `Shora.Application.Common.Results` — never throw for expected business failures.
- Errors use stable string codes in `Shora.Application.Common.ErrorCodes` and an `ErrorKind` that maps to HTTP status.
- Controllers translate results via `ApiControllerBase.FromResult()` / `ToProblem()` — no ad-hoc `{ title: "..." }` payloads.

## 2. Problem Details (RFC 7807)

- All error responses use `ProblemDetails` or `ValidationProblemDetails`.
- Every problem includes extension `code` (machine-readable, e.g. `auth.invalid_credentials`).
- `type` URI pattern: `https://shora.dev/errors/{code}`.
- Model validation failures return `ValidationProblemDetails` with field-level `errors`.
- Unhandled exceptions are caught by `GlobalExceptionHandler` and returned as Problem Details (500).

## 3. API versioning & routes

- URL versioning: `/api/v{version}/...` (default **v1.0**).
- Controllers inherit `ApiControllerBase` with `[ApiVersion("1.0")]`.
- Frontend `apiBaseUrl`: `/api/v1`.

## 4. OpenAPI endpoint metadata

Every action declares:

- `[EndpointName("Resource.Action")]` — stable operation id
- `[EndpointSummary("...")]` — short description
- `[ProducesResponseType(...)]` — success DTO and each error status (`ProblemDetails`)

## 5. Shared contracts (backend ↔ frontend)

- C# DTOs live in **`Shora.Contracts`** (records, no dependencies).
- TypeScript mirrors live in **`src/contracts/`** — keep in sync manually for MVP.
- Frontend imports via `@contracts/*` path alias.
- Application internal types (e.g. `AuthResult` with raw refresh token) stay in Application — not exposed in Contracts.

## 6. Central package management

- `src/backend/Directory.Build.props` — shared MSBuild properties (`net10.0`, nullable, CPM).
- `src/backend/Directory.Packages.props` — single source of truth for NuGet versions.

## 7. Checklist for new endpoints

1. Add request/response records to `Shora.Contracts` + `src/contracts/`.
2. Service method returns `Result<T>`.
3. Controller action: version route, name, summary, `ProducesResponseType`.
4. Integration test covers success + primary failure path.
5. Frontend uses contract types and `readApiError()` for Problem Details.

## 8. Caching

MVP uses **in-process memory caching** on a single App Service instance. Application services depend on `ICacheService`; HTTP output cache applies to anonymous public GET endpoints. A future multi-instance deployment can swap in `IDistributedCache` (Redis) without changing service callers.

### Cacheable endpoints

| Endpoint | App cache key | Output cache policy | Default TTL |
|---|---|---|---|
| `GET /api/settings/public` | `settings:public` | `PublicSettings` | 5 min |
| `GET /api/availability?from=&to=` | `availability:{from}:{to}` | `PublicAvailability` | 30 sec |

**Never cache:** auth endpoints, user-specific lists (`/bookings/mine`), admin paginated queries, payment/receipt flows, or external token validation.

### Configuration

`appsettings.json` section `Cache`:

```json
{
  "Enabled": true,
  "SettingsPublicTtlSeconds": 300,
  "AvailabilityTtlSeconds": 30
}
```

Constants live in `Shora.Application.Common` (`CacheKeys`, `CachePolicies`, `CacheOutputTags`).

### Invalidation

`ICacheInvalidator` clears both application cache entries and output-cache tags:

| Event | Invalidation |
|---|---|
| Admin saves settings | `InvalidatePublicSettingsAsync()` |
| Booking hold/create/cancel, slot freed | `InvalidateAvailabilityAsync()` |
| Admin blocks date / changes availability windows | `InvalidateAvailabilityAsync()` |

Hook invalidation into mutation paths when those features are implemented. `SettingsService.InvalidateCacheAsync()` is the placeholder for admin settings save.

### Controller usage

Apply output cache on public GET actions:

```csharp
[OutputCache(PolicyName = CachePolicies.PublicSettings)]
[HttpGet("settings/public")]
```

Register policies via `AddShoraCaching()`; pipeline includes `UseOutputCache()`.

### Frontend

Use `ApiCacheService` (`src/frontend/src/app/core/api/api-cache.service.ts`) for cacheable GET requests. Cache identity is the request URL (query params normalized), so callers pass `url` + `ttlMs` only — never a separate key.

TTL constants and typed request builders live in `cache.config.ts`:

- `settingsPublicRequest(apiBaseUrl)` — `GET .../settings/public`, 5 min
- `availabilityRequest(apiBaseUrl, from, to)` — `GET .../availability?from=&to=`, 30 sec

Example:

```typescript
const req = settingsPublicRequest(environment.apiBaseUrl);
return this.cache.getCached<PublicSettings>(req.url, req.ttlMs);
```

Call `invalidateUrlPrefix(\`${environment.apiBaseUrl}/availability\`)` when the user enters the booking reserve/confirm step so checkout sees fresh slots after warm browsing.

Failed GETs are not cached: the service evicts the entry on HTTP error so the next call retries immediately.

Do **not** route auth calls through `ApiCacheService`. No service worker for MVP.

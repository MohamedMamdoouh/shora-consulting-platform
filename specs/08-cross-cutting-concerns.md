# 08 — Cross-Cutting Concerns (Ops, Security, Deployment)

Status: **Spec only — not implemented until explicitly requested.**

This spec consolidates operational and cross-cutting requirements referenced by specs 01–07 so they live in one place: rate limiting, logging/auditing/monitoring, the background-job execution model, deployment, and data retention. Nothing here changes feature behavior; it hardens how the system runs in production.

## 1. Rate Limiting (H2)

Uses ASP.NET Core's built-in rate limiting middleware. Limits are per-endpoint and combine a **per-IP** partition and, where an account is identifiable, a **per-account/email** partition. Values below are starting points (tunable via configuration).

| Endpoint(s) | Limit (starting point) | Notes |
|---|---|---|
| `POST /api/auth/login`, `/signup`, `/google` | ~5 / minute / IP + per-email | Temporary lockout / backoff after repeated failures |
| `POST /api/auth/forgot-password`, `/reset-password`, `/verify-email`, `/resend-verification` | ~3–5 / minute / IP + per-email | Still returns generic responses (no account-existence leak) |
| `POST /api/auth/refresh` | ~10 / minute / IP | Blunts brute-force of stolen refresh cookies |
| `GET /api/availability` | ~30 / minute / IP | Public, cacheable; also bounded by date-range validation (spec 04 §2) |
| `POST /api/bookings` | ~10 / minute / account | Backstops the 3-concurrent-hold cap (spec 04) |
| `POST /api/payments/{bookingId}/receipt` | ~5 / minute / account | Bounds receipt-upload abuse; combined with the 5 MB size cap and content-type allowlist (spec 05 §4) |
| `POST /api/bookings/{id}/cancellation-requests` | ~5 / minute / account | Light limit; single reopen policy still blocks spam (spec 04 §3.1) |

- Throttled responses return `429 Too Many Requests` with a `Retry-After` header.
- Admin endpoints sit behind auth + the single admin account, so they need only light limiting.

### Client-IP resolution & shared-IP fairness (resolved)

- **Trusted proxy config:** the app sits behind the host's reverse proxy/CDN, so the raw socket address is the proxy, not the client. ASP.NET Core **forwarded-headers middleware** is configured with the host's known proxy list so `X-Forwarded-For` is honored **only from trusted proxies** — a client can never spoof its own IP by sending the header directly. Per-IP partitions key on this resolved client IP.
- **CGNAT / shared-IP reality (mobile networks in Egypt):** many legitimate users can share one public IP, so per-IP limits are deliberately **coarse safety nets** (they stop floods, not individuals) and the meaningful per-user fairness comes from the **per-account/per-email partitions** above. If 429s from shared IPs show up in monitoring, raise the per-IP values before touching per-account ones.

## 2. Logging, Auditing & Monitoring (H6)

### Structured logging
- Structured (JSON) logs with a **correlation id** per request; payment flows additionally carry the `bookingId`/`paymentId` so a whole transaction can be traced across the upload → review → refund lifecycle.
- **Never log** secrets, JWTs, refresh tokens, or storage connection strings/SAS tokens. Client PII (email/phone) is logged only where necessary and avoided in favor of ids. Receipt images are never logged; only the fact that a SAS URL was minted (with actor + booking id) is recorded.

### Audit trail
- `BookingStatusAudit` (spec 01) records every booking transition (actor, from/to, reason, UTC time) inside the same DB transaction as the change — the authoritative history behind spec 06 labels and spec 07 monitoring, including cancellation-request decisions.
- Payment lifecycle changes are captured via `Payment` fields (`Status`, `UpdatedAt`, refund fields) and `PaymentReceipt`/`CancellationRequest` review fields, plus logs.

### Monitoring & alerts
- **Receipts awaiting review:** alert when a booking sits in `PendingApproval` beyond a threshold (a client is waiting on the admin).
- **Cancellation requests:** alert when a `Pending` cancellation request is approaching its `AutoDeclineAtUtc` so the admin decides deliberately.
- **Refunds due:** alert when a cancelled booking's payment stays `Approved` (refund owed) beyond a threshold.
- **Background jobs:** alert on job exceptions or a job not completing within its expected window.
- **Concrete thresholds (resolved):**
  - `PendingApproval > 6 h` = warning; `> 24 h` = critical.
  - Cancellation request within `< 30 min` of `AutoDeclineAtUtc` = warning.
  - `refund-due > 24 h` = warning; `> 72 h` = critical.
  - Job heartbeat missing for >2 expected intervals = warning; >4 intervals = critical.
  - Outbox: any message reaching `DeadLettered` (spec 01 §4) = warning (it names the message type + aggregate id so the admin can act); `>= 5` dead-lettered in 1 hour = critical (systemic delivery failure, e.g. email provider outage).
- MVP delivery can be as simple as error-level logs shipped to the hosting provider's log sink plus email alerts to the admin; a full APM is out of scope.

### Alert runbooks (resolved)

Every alert above must map to an operator runbook with owner + response SLA:

- `PendingApproval` backlog: validate mail delivery + admin queue health, then force-prioritize review.
- Cancellation requests near auto-decline: page admin and surface queue in dashboard immediately.
- `refund-due` ageing: reconcile manual transfer logs and either record (`refunds/record`) or escalate.
- Job heartbeat missing: verify lease row, app instance health, and last successful run marker.
- Outbox dead-letter: inspect `LastError`, fix template/provider issue, requeue via operator action.

## 3. Background Jobs — Execution Model (M6)

- Each job is a hosted `BackgroundService` in the Api process.
- **Concurrency safety:** a **DB-based lease/lock** (a row a job must acquire, with an expiry) ensures that if the app ever runs on more than one instance, a given job executes on only one instance at a time. All slot generation (on-save + top-up) shares this lease so it stays single-threaded (spec 01 §4, spec 07 §2).
- **Idempotency:** every job is safe to run repeatedly — it re-derives state from the DB rather than assuming a prior run's effects, and each transition is guarded by `Booking.RowVersion`.
- **Reliable side effects:** state-changing transactions write side effects to `OutboxMessage`; dispatcher jobs perform delivery and mark completion, so delivery cannot be lost between DB commit and external send.
- **External-storage consistency:** blob storage is outside SQL transactions. Receipt processing uses explicit recovery states (`BlobFinalizePending`, `Missing`) and repair jobs rather than assuming distributed atomicity.

| Job | Interval | Purpose | Cross-ref |
|---|---|---|---|
| Receipt-upload-deadline cleanup | ~1 min | Cancel `PendingPayment` holds past `ReceiptUploadDeadlineUtc`, free the slot, set `Payment.Status = Void` (never touches `PendingApproval`) | spec 04 §5, spec 05 §8 |
| Cancellation-request auto-decline | ~1 min | Set `Pending` cancellation requests to `AutoDeclined` and return the booking to `Confirmed` at `AutoDeclineAtUtc` | spec 04 §3.1, spec 07 §4.1 |
| Receipt blob reconciliation | ~15 min | Repair `BlobFinalizePending` records and clean orphan temp blobs after partial upload failures | spec 05 §2, spec 05 §8 |
| Auto-complete | ~5 min | `Confirmed` → `Completed` once slot end passed | spec 06, spec 07 |
| Availability top-up | nightly | Keep ~4 weeks of future slots materialized, skipping `BlockedDate`s | spec 07 §2 |
| Receipt retention purge | daily | Remove/scrub receipt blobs and PII metadata older than `Settings.ReceiptRetentionMonths` where not legally held | spec 01 §4, §5 |
| Refresh-token purge | daily | Delete expired `RefreshToken` rows | spec 02 §11 |
| Outbox dispatcher | ~1 min | Deliver pending outbox messages (emails) with retry/backoff; dead-letters after 8 attempts (spec 01 §4) and alerts (§2) | spec 01, spec 05, spec 07 |

## 4. Deployment

- **Receipt image storage:** an Azure Blob Storage account with a **private** container (`Storage:ReceiptContainer`) must be provisioned; the app writes receipt images there and mints short-lived SAS read URLs for admin viewing (spec 05 §4). No public blob access.
- **Upload safety controls:** malware scanning service must be provisioned/integrated for uploaded receipts before admin review is allowed.
- **Database migrations on deploy:** EF Core migrations (spec 01 §5) are applied automatically as part of the deploy/startup so schema stays in sync; seed data (Settings row, admin user, roles) runs once, idempotently.
- **Secrets:** all secrets (`ConnectionStrings`, `Jwt:SigningKey`, `Storage:ConnectionString`, `Google:*`, `Email:*`) come from environment variables / the host's secret store — never committed. Local dev uses `dotnet user-secrets` (spec 01 §2.2).
- **Frontend/API site model (resolved):** MVP deployment is **same-site** (frontend and API under the same registrable domain over HTTPS). This is required by the `SameSite=Strict` refresh cookie strategy in spec 02.
- **CORS:** the API allows only the Angular app's origin(s) and `AllowCredentials` (required so the refresh-token `httpOnly` cookie flows on `POST /api/auth/refresh`, spec 02). Wildcard origins are not used with credentials.
- **HTTPS everywhere + secure cookies:** the refresh-token cookie is `Secure`/`httpOnly`/`SameSite=Strict`, which requires HTTPS in all deployed environments.
- **Hosting decision (resolved):** MVP uses an **always-on web app/container host** (not scale-to-zero serverless) plus managed SQL Server/Azure SQL and Azure Blob Storage. Required capabilities: .NET 10 runtime, scheduled/background processing in-process. No public inbound webhook endpoint is required (payments are verified manually).

## 5. Data Retention & PII (L4)

MVP stance — minimal and documented, not a full compliance program:

- **Data collected:** account email + display name (may be a pseudonym, SDD §5.7), per-booking contact phone (Voice Call only), booking/payment records, and **uploaded receipt images** (which may show the client's name / wallet number). No card data is ever collected or stored (there is no card gateway).
- **Retention:** booking, payment, and audit records are retained indefinitely for MVP (needed for earnings history and dispute/refund traceability). Receipt images are retained per `Settings.ReceiptRetentionMonths` (default 24), then securely purged/scrubbed by the retention job unless a legal/dispute hold exists. Refresh tokens are purged after expiry (§3).
- **Receipt access:** receipt images are visible only to the admin, only via short-lived SAS URLs (spec 05 §4); they are never exposed on a public URL or to other clients.
- **Access:** client data is only ever visible to that client and the single admin (spec 06 §5). No third-party analytics/PII sharing in MVP.
- **Deletion:** account/data-deletion requests are handled manually by the admin for MVP; a self-serve "delete my account" flow is out of scope.
- **Transport security:** all traffic over HTTPS; secrets and PII never logged in plaintext (§2).

## 6. Out of Scope (MVP)
- Full APM / distributed tracing platform, WAF configuration specifics, and autoscaling policy.
- Automated GDPR/data-subject tooling (handled manually).
- Multi-region / high-availability topology (single instance is sufficient for MVP; the DB-lease model just keeps the door open).

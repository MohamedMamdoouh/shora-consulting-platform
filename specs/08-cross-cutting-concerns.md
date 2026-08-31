# 08 — Cross-Cutting Concerns (Ops, Security, Deployment)

Status: **Done**. Observability, outbox email delivery, all background jobs, full rate-limit matrix, and ops monitoring with runbooks are implemented.

This spec consolidates operational and cross-cutting requirements referenced by specs 01–07: rate limiting, logging/auditing/monitoring, the background-job execution model, deployment, and data retention.

---

## 1. Rate Limiting (H2)

Uses ASP.NET Core's built-in rate limiting middleware. Limits are **per-IP** per endpoint. Values are tunable via `RateLimiting` and `ReceiptUpload` configuration.

| Endpoint(s)                                                                                  | Limit (starting point) | Status   |
| -------------------------------------------------------------------------------------------- | ---------------------- | -------- |
| `POST /api/auth/login`, `/signup`                                                            | ~5 / minute / IP       | **Done** |
| `POST /api/auth/forgot-password`, `/reset-password`, `/verify-email`, `/resend-verification` | ~5 / minute / IP       | **Done** |
| `POST /api/auth/refresh`                                                                     | ~10 / minute / IP      | **Done** |
| `GET /api/availability`                                                                      | ~30 / minute / IP      | **Done** |
| `POST /api/bookings`                                                                         | ~10 / minute / IP      | **Done** |
| `POST /api/payments/{bookingId}/receipt`                                                     | ~5 / minute / IP       | **Done** |
| `POST /api/bookings/{id}/cancellation-requests`                                              | ~5 / minute / IP       | **Done** |

- Throttled responses return `429 Too Many Requests` with a `Retry-After` header.
- Rate limiting runs **before** output cache so cached availability responses still count toward limits.
- Admin endpoints sit behind auth + the single admin account; light limiting only (not matrix-listed).

### Client-IP resolution (resolved)

- **Direct connection:** the app runs on a single server with no load balancer, so per-IP rate-limit partitions key on `HttpContext.Connection.RemoteIpAddress` directly.
- **CGNAT / shared-IP reality (mobile networks in Egypt):** all throttled endpoints share one counter per IP. If 429s from shared IPs show up in monitoring, raise the relevant per-IP limit values in configuration.

## 2. Logging, Auditing & Monitoring (H6)

### Structured logging

- Structured logs with a **correlation id** per request; payment flows additionally carry `bookingId`/`paymentId`.
- **Never log** secrets, JWTs, refresh tokens, or storage connection strings/SAS tokens. Receipt images are never logged.

### Audit trail

- `BookingStatusAudit` (spec 01) records every booking transition inside the same DB transaction as the change.
- Payment lifecycle changes are captured via `Payment` fields, `PaymentReceipt`/`CancellationRequest` review fields, and logs.

### Monitoring & alerts

**`OpsMonitoringService`** evaluates alert conditions every ~5 minutes (`OpsMonitoringJob`) and logs warnings/criticals. **`GET /api/v1/admin/ops/alerts`** (`AdminOpsController`) returns active alerts for the admin dashboard. Runbook IDs match [`OpsRunbookIds`](../src/backend/Shora.Application/Ops/OpsRunbookIds.cs) and [`runbooks.json`](../src/backend/Shora.Application/Ops/runbooks.json).

| Condition                                   | Threshold                               |
| ------------------------------------------- | --------------------------------------- |
| `PendingApproval` backlog                   | > 6 h warning · > 24 h critical         |
| Cancellation request near auto-decline      | < 30 min to `AutoDeclineAtUtc`          |
| Refund due (cancelled + `Payment.Approved`) | > 24 h warning · > 72 h critical        |
| Job heartbeat stale                         | > 2× interval warning · > 4× critical   |
| Job failure                                 | `LastFailureAtUtc` > `LastSuccessAtUtc` |
| Outbox dead-letter                          | any `DeadLettered` message = warning    |
| Outbox dead-letter burst                    | ≥ 5 in 1 hour = critical                |

MVP delivery: structured logs to the hosting provider's log sink. Full APM is out of scope.

### Alert runbooks

Source of truth: [`src/backend/Shora.Application/Ops/runbooks.json`](../src/backend/Shora.Application/Ops/runbooks.json) (embedded in the API; exposed via `GET /api/v1/admin/ops/runbooks` and the admin `/admin/ops` page). See [`docs/ops-runbooks.md`](../docs/ops-runbooks.md) for pointers.

## 3. Background Jobs — Execution Model (M6)

- Each job is a hosted `BackgroundService` in the Api process on the **single app instance**.
- **Idempotency:** every job re-derives state from the DB; transitions guarded by `Booking.RowVersion`.
- **Heartbeats:** `JobHeartbeatService` records success/failure in `JobRunHistory`; ops monitoring alerts on stale heartbeats.
- **Side effects:** state-changing transactions write `OutboxMessage`; the dispatcher delivers emails with retry/backoff.

| Job                                | Interval | Purpose                                                                         | Status   |
| ---------------------------------- | -------- | ------------------------------------------------------------------------------- | -------- |
| Receipt-upload-deadline cleanup    | ~1 min   | Cancel `PendingPayment` holds past deadline, free slot, `Payment.Status = Void` | **Done** |
| Outbox dispatcher                  | ~1 min   | Deliver pending outbox emails; dead-letter after 8 attempts                     | **Done** |
| Cancellation-request auto-decline  | ~1 min   | `Pending` → `AutoDeclined`, booking → `Confirmed` at `AutoDeclineAtUtc`         | **Done** |
| Booking auto-complete              | ~5 min   | `Confirmed` → `Completed` once `SlotEndUtc` passed                              | **Done** |
| Receipt blob reconciliation        | ~15 min  | Repair `BlobFinalizePending` / `Missing`; delete orphan `temp/` blobs > 1 h     | **Done** |
| Ops monitoring                     | ~5 min   | Evaluate operational alerts and log warnings/criticals                          | **Done** |
| Receipt retention purge            | daily    | Purge receipt blobs older than `Settings.ReceiptRetentionMonths`                | **Done** |
| Temp blob cleanup (orphan `temp/`) | daily    | Delete aged orphan temp blobs (24 h default)                                    | **Done** |
| Refresh-token purge                | daily    | Delete expired `RefreshToken` rows                                              | **Done** |
| Availability top-up                | nightly  | Keep ~4 weeks of future slots materialized, skipping `BlockedDate`s             | **Done** |

Disable all jobs in dev/test via `BackgroundJobs:Enabled = false`.

### Configuration keys

Tunable via `appsettings.json` (see `BackgroundJobOptions`, `OpsMonitoringOptions`, `RateLimitOptions`, `EmailOptions`):

| Section                            | Purpose                                                                                                            |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `BackgroundJobs:Enabled`           | Master switch (false in integration tests)                                                                         |
| `BackgroundJobs:*IntervalSeconds`  | Per-job schedule (defaults match table above)                                                                      |
| `OpsMonitoring:*`                  | Alert thresholds (pending approval hours, refund-due hours, heartbeat multipliers, dead-letter burst count/window) |
| `RateLimiting:*`                   | Per-endpoint IP limits                                                                                             |
| `ReceiptUpload:RateLimitPerMinute` | Receipt upload cap (default 5/min/IP)                                                                              |
| `Email:*`                          | Brevo API key, from address                                                                                        |

## 4. Deployment

- **CI/CD:** see [spec 09](09-ci-cd-pipeline.md) — CI on push/PR via GitHub Actions; production deploy on push to `main` via Render (Git + Docker). Operator guide: [docs/deployment.md](../docs/deployment.md).
- **Receipt storage:** private Azure Blob container (`Storage:ReceiptContainer`); short-lived SAS read URLs for admin.
- **Email:** configure `Email:ApiKey` and `Email:FromAddress` for production Brevo; dev uses log sender.
- **Migrations + seed:** applied on startup, idempotent.
- **Secrets:** environment variables / user-secrets — never committed.
- **Same-site deployment:** frontend + API under one registrable domain (required for `SameSite=Strict` refresh cookies).
- **Single always-on instance:** no horizontal scaling in MVP.

## 5. Data Retention & PII (L4)

- Booking, payment, and audit records retained indefinitely for MVP.
- Receipt images retained per `Settings.ReceiptRetentionMonths` (default 24), then purged by the retention job.
- Refresh tokens purged after expiry by the daily purge job.
- Account deletion handled manually by admin for MVP.

## 6. Out of Scope (MVP)

- Full APM / distributed tracing platform and WAF configuration specifics.
- Automated GDPR/data-subject tooling.
- Multi-region / high-availability topology.

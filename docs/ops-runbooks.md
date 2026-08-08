# Shora Ops Runbooks

Operator runbooks for alerts emitted by `OpsMonitoringService` (spec 08 §2). MVP delivery is structured logs plus this reference; wire log-sink alerts to these IDs in production.

| Runbook ID                               | Owner            | Response SLA                            |
| ---------------------------------------- | ---------------- | --------------------------------------- |
| `pending-approval-backlog`               | Admin / operator | Warning: 4 h · Critical: 1 h            |
| `cancellation-request-near-auto-decline` | Admin            | 15 min                                  |
| `refund-due-ageing`                      | Admin / finance  | Warning: 1 business day · Critical: 4 h |
| `job-heartbeat-missing`                  | Operator         | Warning: 30 min · Critical: 15 min      |
| `job-failure`                            | Operator         | 30 min                                  |
| `outbox-dead-letter`                     | Operator         | 1 h                                     |
| `outbox-dead-letter-burst`               | Operator         | 15 min                                  |

## `pending-approval-backlog`

**Trigger:** Booking in `PendingApproval` longer than 6 h (warning) or 24 h (critical).

**Steps:**

1. Confirm admin notification email delivery (outbox + SMTP logs).
2. Open admin bookings queue filtered to `PendingApproval`.
3. Prioritize review of flagged booking(s); approve or decline with reason.
4. If queue is empty but alert persists, check `BookingStatusAudit` vs current status for stale data.

## `cancellation-request-near-auto-decline`

**Trigger:** Pending cancellation request auto-declines within 30 minutes.

**Steps:**

1. Surface the booking in the admin cancellation queue immediately.
2. Approve or decline deliberately before `AutoDeclineAtUtc`.
3. If unreachable, note auto-decline will restore booking to `Confirmed` and email the client.

## `refund-due-ageing`

**Trigger:** Cancelled booking with `Payment.Status = Approved` (refund owed) older than 24 h (warning) or 72 h (critical).

**Steps:**

1. Reconcile manual transfer logs (Vodafone Cash / InstaPay).
2. Record refund via `POST /api/v1/admin/payments/{id}/refunds/record` when transfer completed.
3. Escalate if client contact is needed or amount is disputed.

## `job-heartbeat-missing`

**Trigger:** Background job last success older than 2× (warning) or 4× (critical) its configured interval.

**Steps:**

1. Verify app process health and that `BackgroundJobs:Enabled` is true.
2. Inspect `JobRunHistory` for the named job (`LastSuccessAtUtc`, `LastFailureAtUtc`, `LastError`).
3. Check application logs around the last failure; restart the app if the worker loop stopped.
4. After recovery, confirm the next heartbeat updates `LastSuccessAtUtc`.

## `job-failure`

**Trigger:** Job recorded `LastFailureAtUtc` newer than `LastSuccessAtUtc`.

**Steps:**

1. Read `LastError` in `JobRunHistory` for the job name.
2. Fix root cause (DB, storage, email provider, code defect).
3. Wait for the next scheduled run or restart the app; confirm success heartbeat.

## `outbox-dead-letter`

**Trigger:** Any `OutboxMessage` in `DeadLettered` status.

**Steps:**

1. Inspect `LastError`, `MessageType`, and `AggregateId` for the message.
2. Fix template, recipient, or provider configuration.
3. Requeue via explicit operator action (manual resend / new outbox row per runbook in spec 01).

## `outbox-dead-letter-burst`

**Trigger:** ≥ 5 messages dead-lettered within 1 hour (systemic failure).

**Steps:**

1. Treat as provider outage (SMTP, DNS, credentials).
2. Pause non-essential deploys; verify `Email:*` configuration and provider status.
3. Clear backlog incrementally after provider recovery; monitor for recurrence.

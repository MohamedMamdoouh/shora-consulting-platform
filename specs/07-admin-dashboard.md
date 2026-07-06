# 07 — Admin / Consultant Dashboard

Status: **Spec only — not implemented until explicitly requested.**

## 1. Purpose

The single consultant's control panel: manage availability, edit the session price/duration, view and manage bookings, and see a basic earnings overview. Accessible only to the seeded `Admin` account (per spec 02).

## 2. Page: `/admin/availability`

- **Recurring windows editor**: manage `AvailabilityWindow` rows (day of week + start/end time), e.g. "Monday 16:00–21:00". Times are entered in the consultant's local time (**Africa/Cairo**, confirmed) and converted to UTC at slot generation (spec 01 §4). CRUD via:
  - `GET /api/admin/availability-windows`
  - `POST /api/admin/availability-windows`
  - `PUT /api/admin/availability-windows/{id}`
  - `DELETE /api/admin/availability-windows/{id}`
- **Slot generation (confirmed)**: whenever windows are added/edited, the backend immediately (on-save) regenerates concrete `AvailabilitySlot` rows for the next **4 weeks**. Existing booked slots are never removed/regenerated — only unbooked slots within the horizon are recalculated. **Packing rule:** within each window, slots are laid out as `SessionDurationMinutes` back-to-back with `BufferMinutes` between them; a trailing remainder too small for a full session is dropped. Generation is **serialized/idempotent** and shares the DB lease with the background jobs (spec 01, spec 08) so on-save and the top-up job cannot create duplicate slots (the unique index on `StartTime` is the backstop).
- **Horizon top-up**: a recurring background job (**nightly**, spec 08 intervals) extends slot materialization so there are always ~4 weeks of future slots, even when windows haven't been edited. This is also how mid-cycle changes to `SessionDurationMinutes`/`BufferMinutes` propagate to future **unbooked** slots.
- **BlockedDate awareness**: all slot generation (on-save and top-up) **skips ranges covered by a `BlockedDate`** — blocked ranges are never re-materialized.
- **Block specific dates** (backed by the `BlockedDate` entity — spec 01 §4): mark a date/time range as unavailable (e.g. vacation). CRUD via:
  - `GET /api/admin/blocked-dates`
  - `POST /api/admin/blocked-dates` — body: `{ startUtc, endUtc, reason? }`
  - `DELETE /api/admin/blocked-dates/{id}`
  - Creating a block is **rejected** if the range overlaps any slot tied to a booking in an active state (`PendingPayment`/`PendingApproval`/`Confirmed`/`CancellationRequested`/`Completed`) (the admin cannot block a date a client reserved). The error identifies the conflicting booking(s) so the admin can cancel them first if they truly intend to block that range. When the range is free, overlapping unbooked `AvailabilitySlot`s are removed (booking history is unaffected — dashboards read the snapshotted `SlotStartUtc`/`SlotEndUtc` on the booking, spec 01).
  - **Atomicity (resolved):** the overlap check and the removal of unbooked slots run in **one DB transaction** that takes update locks on the slots in the range (e.g. `UPDLOCK, HOLDLOCK` on `AvailabilitySlot` rows whose `StartTime` falls inside the block). A client reserve (`UPDATE ... WHERE IsBooked = 0`) racing this transaction either commits first (block creation then fails the overlap check) or blocks and finds the slot row gone. No check-then-act window exists in which a client books a slot the admin is simultaneously blocking.

## 3. Page: `/admin/settings`

- Edit `SessionPrice` (default 500 EGP), `SessionDurationMinutes` (default 60), `BufferMinutes` (default 15), `ReceiptUploadWindowMinutes` (default 60), `CancellationRequestAutoDeclineHours` (default 1), `ConsultantWhatsAppNumber`, `VodafoneCashNumber`, `InstaPayHandle`, and the optional `PaymentInstructions` note via:
  - `GET /api/admin/settings`
  - `PUT /api/admin/settings`
- **Validation surfaced in the UI (H4):** the form enforces the same constraints defined in spec 01 §4 — `SessionPrice > 0` (max 2 decimals), `SessionDurationMinutes` 30–240, `BufferMinutes ≥ 0`, `ReceiptUploadWindowMinutes ≥ 5`, `CancellationRequestAutoDeclineHours ≥ 0`, `ConsultantWhatsAppNumber` a valid E.164 number (e.g. `+2010xxxxxxxx`), `VodafoneCashNumber` a valid Egyptian mobile number, and `InstaPayHandle` non-empty. Invalid input is rejected by both the client form and `PUT /api/admin/settings` (server is authoritative) with field-level error messages; a bad value can never be persisted. The Vodafone Cash number and InstaPay handle are shown to clients in the payment instructions (spec 05), so they must be kept current.
- **Singleton semantics (resolved):** this endpoint always targets the singleton row `Settings.Id = 1`; no create/delete settings actions exist in the API.
- Changing these does **not** retroactively affect existing bookings (price is snapshotted on `Payment.Amount` at reserve time; duration/buffer changes only affect newly generated slots going forward).

## 4. Page: `/admin/bookings`

- Table of all bookings, filterable by status (`PendingPayment`, `PendingApproval`, `Confirmed`, `CancellationRequested`, `Completed`, `Cancelled`) and date range.
- Each row shows: client display name, **client contact phone** (only present for Voice Call bookings; visible only to the admin and the owning client per spec 01 §4 — never to other clients), delivery method, slot time (read from the booking's snapshotted `SlotStartUtc`, spec 01; converted from UTC to the admin's local browser timezone per spec 01 §4), status, and — for cancelled rows — the reason from the audit trail (spec 06).
- **Pagination (M5):** `GET /api/admin/bookings?status=&from=&to=&page=&pageSize=` returns `{ items, page, pageSize, totalCount }` (default `pageSize` 20, max 100), most-recent-first. The list never loads unbounded.
- **Receipt review (`PendingApproval`):** rows awaiting review expose **View receipt / Approve / Decline** actions.
  - `GET /api/admin/bookings/{id}/receipts` returns the `PaymentReceipt` attempt history with short-lived SAS image URLs (spec 05 §4).
  - `POST /api/admin/bookings/{id}/receipts/approve` → booking `Confirmed`, confirmation emails enqueued.
  - `POST /api/admin/bookings/{id}/receipts/decline` (body `{ reasonCode, reasonNote? }`) → booking back to `PendingPayment` with a fresh upload window; a "please re-upload" email carries the typed reason + optional note.
- Bookings **auto-complete**: a background job transitions `Confirmed` bookings to `Completed` once the booking's snapshotted `SlotEndUtc` passes (spec 01 §4 / spec 06). There is no manual "mark completed" action.
- **Direct cancel** action: `POST /api/admin/bookings/{id}/cancel` — allowed **any time before the session start**, from `Confirmed`, `CancellationRequested`, or an unpaid hold. Sets `Booking.Status = Cancelled` (writing a `BookingStatusAudit` row), frees the `AvailabilitySlot`. If the payment is `Approved`, this creates a **refund-due** (see below); otherwise the payment is set `Void`. A booking that has already started/completed cannot be cancelled (`now < StartUtc` guard, spec 04 §4).
- **Cancellation-request decisions:** for `CancellationRequested` bookings the admin approves or declines the client's request (see §4.1). Approval is equivalent to a direct cancel (refund-due if paid); decline returns the booking to `Confirmed`.
- **Manual refunds (`refunds/record`):** because refunds are out-of-band (spec 05 §6), a cancelled booking whose payment is still `Approved` shows a **"refund due"** indicator. The admin sends the money back via Vodafone Cash/InstaPay, then records it with `POST /api/admin/payments/{id}/refunds/record` (body `{ reference, note }`), which sets `Payment.Status = Refunded` and enqueues the client refund email. If recorded by mistake, `POST /api/admin/payments/{id}/refunds/revoke` appends correction audit and reopens refund-due.

### 4.1 Cancellation-request queue

- A view/filter for `CancellationRequested` bookings shows each request's client reason, request time, and a **countdown to `CancellationRequest.AutoDeclineAtUtc`** (spec 01), so the admin can act deliberately before the safety-net auto-decline fires.
- **Approve:** `POST /api/admin/bookings/{id}/cancellation-requests/approve` → `CancellationRequest.Status = Approved`, booking `Cancelled`, slot freed, refund-due if the payment is `Approved`; client cancellation/refund email enqueued.
- **Decline:** `POST /api/admin/bookings/{id}/cancellation-requests/decline` (body `{ reasonCode, reasonNote? }`) → `CancellationRequest.Status = Declined` with typed reason + note, booking back to `Confirmed`; "request declined — session stands" email enqueued.
- **Auto-decline:** if the admin does nothing, the auto-decline job (§7 / spec 08) sets the request to `AutoDeclined` and returns the booking to `Confirmed` at `AutoDeclineAtUtc` (actor `System`).
- **Reopen policy:** exactly one client reopen is allowed after an admin decline while still before the deadline (`ReopenCount <= 1`); no reopen after auto-decline.

## 5. Page: `/admin/earnings`

- Basic aggregate view with explicit financial semantics. A refunded payment's status is `Refunded` (no longer `Approved`), so gross must count **every approved payment regardless of later refund state** or refunds would be double-subtracted:
  - `grossRevenue`: sum of `Payment.Amount` where `Payment.Status IN (Approved, Refunded)` — i.e. all payments the admin ever approved.
  - `refundedAmount`: sum of `Payment.Amount` where `Payment.Status = Refunded`.
  - `netRevenue`: `grossRevenue - refundedAmount`.
  - counts: `approvedCount`, `refundedCount`, and `refundDueCount` (cancelled bookings whose payment is still `Approved` — a manual refund is owed).
- `GET /api/admin/earnings?from=&to=` returns all metrics above so accounting is unambiguous (gross vs net is never inferred).

## 6. Notifications (email only)

**Email is the only notification channel — no SMS anywhere in the system.** All notifications are delivered via the email provider configured under `Email:*` (spec 01), the same one used for password-reset emails.

- **Receipt uploaded (to admin):** when a client uploads a receipt (booking → `PendingApproval`, spec 05), the admin is emailed that a payment needs review.
- **Booking confirmation (to client):** when the admin approves the receipt and the booking becomes `Confirmed` (spec 05 §2), the client receives a confirmation email with the date/time and delivery instructions (call time or `wa.me` link).
- **New booking (to admin):** at the same moment (approval), the backend emails the admin the booking details (client display name, contact phone if Voice Call, delivery method, slot time).
- **Receipt declined (to client):** when the admin declines a receipt, the client is emailed the reason and asked to re-upload before the new deadline (spec 05).
- **Cancellation request received (to admin):** when a client submits a cancellation request (spec 04 §3.1), the admin is emailed to review it before the auto-decline deadline.
- **Cancellation-request decision (to client):** on approve (cancelled + refund to follow), decline, or auto-decline, the client receives the corresponding email; a declined/auto-declined request notes the session stands.
- **Cancellation + refund (to client):** on any cancellation (client abandon, request approval, or admin direct cancel), the client receives a cancellation email; if a refund is owed, a refund confirmation follows once the admin records `refunds/record` (spec 05 §6).
- **Email verification + password reset (to client):** the signup verification link (spec 02 §7) and password-reset link (spec 02 §6) use the same provider.
- **Delivery model (H5):** all of the above are written to `OutboxMessage` inside the same DB transaction as the state change, keyed by `(bookingId, emailType)`, then dispatched asynchronously with retry/backoff so job retries never double-send and a mail-provider outage never blocks a state change (spec 05, spec 08).

## 7. Auditing, Monitoring & Background Jobs (H6, M6)

- **Status-change audit trail:** every booking transition is recorded in `BookingStatusAudit` (spec 01) with actor (Client/Admin/System), reason, and UTC timestamp. This backs the cancelled-reason labels (spec 06) and gives the admin an authoritative history, including cancellation-request decisions.
- **Payment/refund logging:** every payment action (receipt upload, admin approve/decline, manual `refunds/record`/`refunds/revoke`) is logged with a correlation id tied to the booking/payment (details in spec 08). Receipt image access (SAS URL minting) is logged too.
- **Alerts:** operational alerts for (a) bookings sitting in `PendingApproval` beyond a threshold (client waiting on review), (b) cancellation requests approaching their `AutoDeclineAtUtc` (so the admin decides deliberately rather than letting them lapse), (c) cancelled bookings with a `refund-due` older than a threshold, and (d) background-job failures. Delivered to the admin (email/log sink per spec 08).
- **Alert thresholds (resolved):** `PendingApproval > 6 h` = warning, `> 24 h` = critical; cancellation request within `< 30 min` of `AutoDeclineAtUtc` = warning; `refund-due > 24 h` = warning, `> 72 h` = critical.
- **Background-job intervals (M6):** the receipt-upload-deadline cleanup job runs ~**every 1 minute**, the cancellation-request auto-decline job ~**every 1 minute**, the auto-complete job ~**every 5 minutes**, and the availability top-up job **nightly**. All jobs run as hosted services guarded by a DB lease so they are safe if the app scales to multiple instances (spec 01 §2.1, spec 08).

## 8. Open Items for This Area

- None.

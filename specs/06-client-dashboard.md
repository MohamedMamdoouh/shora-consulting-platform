# 06 — Client Dashboard

Status: **Spec only — not implemented until explicitly requested.**

## 1. Purpose

A simple view for logged-in clients to see their booking history and upcoming session details, to complete payment (upload a transfer receipt) for a pending booking, and to request cancellation of an upcoming confirmed session (spec 04 §3). No self-service rescheduling — a client who wants a different time asks the consultant to cancel and then rebooks.

## 2. Page: `/dashboard`

- **Upcoming section**: bookings with status `Confirmed` and a future slot time. Times are converted from UTC to the visitor's local browser timezone (per spec 01 §4).
  - Shows: date/time, delivery method, and delivery instructions:
    - Voice Call: "You'll receive a call at 4:00 PM on [contactPhone]."
    - Chat: a tappable pre-filled `wa.me` link (per spec 04, confirmed mechanic) to start the WhatsApp chat at the scheduled time.
  - **Request cancellation button** — shown only while `now < SlotStartUtc - Settings.CancellationRequestAutoDeclineHours` (default 1h). Calls `POST /api/bookings/{id}/cancellation-requests` (spec 04 §3.1) with an optional reason; a confirmation prompt explains that the consultant must approve it and that an approved cancellation is fully refunded manually.
  - **Once past the deadline** (or the session is within the auto-decline window): the button is hidden/disabled and replaced with a note: "To cancel this close to your session, please contact the consultant on WhatsApp: [ConsultantWhatsAppNumber]."
  - **While a request is pending** (`CancellationRequested`): the card shows "Cancellation requested — awaiting the consultant's decision" and no duplicate action.
  - **After a declined/auto-declined request**: the booking is back to `Confirmed`; a one-time note shows "Your cancellation request was declined — the session stands" with the reason from the audit trail.
    - The one-time behavior is server-driven: the banner remains until `POST /api/bookings/{id}/cancellation-requests/decision-seen` is called, which sets `CancellationRequest.ClientDecisionSeenAtUtc`.
    - Re-request rule: if the decline was admin-driven and the user has not used their single reopen yet, the request button is shown again; otherwise it is hidden and the client is directed to WhatsApp.
- **Pending payment / awaiting review**:
  - `PendingPayment` booking (within its upload window, spec 04 §4): show the **payment instructions** (Vodafone Cash number, InstaPay handle, exact amount, optional note from `GET /api/bookings/{id}/payment-instructions`), a countdown to the upload deadline, and an **Upload receipt** control (`POST /api/payments/{bookingId}/receipt`, spec 05). If a previous attempt was declined, show the **decline reason** and allow a fresh upload. A **Cancel hold** button (`POST /api/bookings/{id}/cancel`) is available any time — it releases the slot and frees a unit of the 3-hold cap immediately.
  - `PendingApproval` booking (receipt uploaded): show "Payment under review — we'll email you once the consultant approves it," with a thumbnail of the submitted receipt. **Cancel hold** remains available.
- **Past section**: bookings with status `Completed` or `Cancelled`, most recent first. Shows date/time and final status. Note: `Completed` appears **automatically** once the session end time has passed (spec 07) — no action needed from the client or admin.
  - **Cancelled bookings show a reason label** derived from the latest `BookingStatusAudit` row (spec 01): e.g. "Cancelled by you", "Cancelled by the consultant", or "Receipt not uploaded in time". (A *declined* cancellation request does not appear here — that booking stays `Confirmed`.) If the cancelled booking had an approved payment, the label also notes "Refunded" once `Payment.Status = Refunded`, or "Refund being processed" while it is still refund-due (spec 05 §6).

## 3. Endpoint

`GET /api/bookings/mine` (Client, auth required) — returns the current user's bookings, each with the **snapshotted** slot time (`Booking.SlotStartUtc`/`SlotEndUtc`, spec 01 — never a live slot join, so history survives slot removal), delivery method, status, and (for cancelled ones) the cancellation reason label (L2). The Upcoming/Pending sections are naturally small, but the Past section grows unboundedly, so the endpoint is **paginated (M5)**:

- Query params: `?status=` (optional filter: `upcoming` = `Confirmed`/`CancellationRequested`; `pending` = `PendingPayment`/`PendingApproval`; `past` = `Completed`/`Cancelled`), `?page=` (1-based, default 1), `?pageSize=` (default 20, max 100).
- Response envelope: `{ items: [...], page, pageSize, totalCount }`, ordered most-recent-first for past bookings. Each item includes cancellation-request metadata (`status`, `reopenCount`, `clientDecisionSeenAtUtc`) so the UI can render pending/declined/reopen states deterministically.
- The frontend requests upcoming/pending unpaginated (small) and pages through the past list.

## 4. Empty State

First-time clients with no bookings see a friendly empty state with a CTA back to `/services` or `/booking/start` — keeps tone warm, not clinical (per SDD §4).

## 5. Privacy Note

This dashboard only ever shows the logged-in client's own data — no visibility into other clients' bookings, consultant's full calendar, or any other client's identity (reinforces SDD §5.7).

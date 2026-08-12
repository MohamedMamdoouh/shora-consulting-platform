# Shared API contracts

TypeScript definitions in this folder mirror `src/backend/Shora.Contracts` (C# records).

When adding or changing an API request/response:

1. Update the C# record in `Shora.Contracts`.
2. Update the matching TypeScript interface here (same property names; camelCase in TS, PascalCase in C# — JSON serialization uses camelCase by default).
3. Import from `@contracts/*` in the Angular app.

This keeps the frontend and backend aligned without code generation for MVP.

**Booking:** `booking.ts` includes reserve/create types, `MyBookingListItem`, `MyBookingsResponse`, cancellation-request DTOs, admin bookings list types (`AdminBookingListItem`, `AdminBookingsResponse`), admin cancellation DTOs, and query limits for `GET /bookings/mine` and `GET /admin/bookings`.

**Payments:** `payments.ts` includes receipt upload/review, admin refund record/revoke types, and receipt decline reason codes.

**Earnings:** `earnings.ts` includes `AdminEarningsResponse` and query types for `GET /admin/earnings`.

**Settings:** `settings.ts` includes `PublicSettings`, `AdminSettings`, and `UpdateAdminSettingsRequest` for admin settings CRUD.

**Availability:** `availability.ts` includes admin window CRUD types (`AvailabilityWindow`, `CreateAvailabilityWindowRequest`, `UpdateAvailabilityWindowRequest`) and blocked-date types.

**Ops:** `ops.ts` includes `AdminOpsAlertDto`, `AdminOpsAlertsResponse`, `AdminOpsRunbookDto`, and `AdminOpsRunbooksResponse` for `GET /admin/ops/alerts` and `GET /admin/ops/runbooks`.

`ProblemDetails` in `common.ts` documents the RFC 7807 error JSON shape (ASP.NET framework type, not a C# Contracts record). `error-codes.ts` mirrors `Shora.Application.Common.ErrorCodes`. `error-catalog.ts` mirrors error reference API responses from `GET /api/v1/errors`.

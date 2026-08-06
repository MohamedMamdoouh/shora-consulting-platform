# Shared API contracts

TypeScript definitions in this folder mirror `src/backend/Shora.Contracts` (C# records).

When adding or changing an API request/response:

1. Update the C# record in `Shora.Contracts`.
2. Update the matching TypeScript interface here (same property names; camelCase in TS, PascalCase in C# — JSON serialization uses camelCase by default).
3. Import from `@contracts/*` in the Angular app.

This keeps the frontend and backend aligned without code generation for MVP.

**Booking (spec 04/06/07):** `booking.ts` includes reserve/create types, `MyBookingListItem`, `MyBookingsResponse`, cancellation-request DTOs, admin bookings list types (`AdminBookingListItem`, `AdminBookingsResponse`), admin cancellation DTOs, and query limits for `GET /bookings/mine` and `GET /admin/bookings`.

**Payments (spec 05/07):** `payments.ts` includes receipt upload/review, admin refund record/revoke types, and receipt decline reason codes.

**Earnings (spec 07):** `earnings.ts` includes `AdminEarningsResponse` and query types for `GET /admin/earnings`.

**Settings (spec 07):** `settings.ts` includes `PublicSettings`, `AdminSettings`, and `UpdateAdminSettingsRequest` for admin settings CRUD.

**Availability (spec 07):** `availability.ts` includes admin window CRUD types (`AvailabilityWindow`, `CreateAvailabilityWindowRequest`, `UpdateAvailabilityWindowRequest`) and blocked-date types.

`ProblemDetails` in `common.ts` documents the RFC 7807 error JSON shape (ASP.NET framework type, not a C# Contracts record). `error-codes.ts` mirrors `Shora.Application.Common.ErrorCodes`.

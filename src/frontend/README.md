# Shora Web (Angular 21)

Arabic-first (RTL) frontend for the Shora booking platform. API contracts live in `src/contracts/`; HTTP calls use `@contracts/*` types aligned with `Shora.Contracts`.

## Development server

```bash
npm install
npm start
```

Open `http://localhost:4200/`. The dev server proxies `/api` to the backend (`proxy.conf.json`) so auth cookies work on same-origin `localhost:4200`.

## Implemented features (by spec)

| Area | Route(s) | Spec |
| ---- | -------- | ---- |
| Auth | `/auth/*` | 02 |
| Public pages | `/`, `/about`, `/services` | 03 (placeholder copy) |
| Booking flow | `/booking/start` → delivery → phone → review → `/booking/payment/:id` | 04 |
| Client dashboard | `/dashboard` | 06 |
| Admin dashboard | `/admin/settings`, `/admin/availability`, `/admin/bookings`, `/admin/earnings` | 07 |

### Booking flow (spec 04)

- Slot picker, delivery method, contact phone (voice call), review & reserve
- Post-reserve payment instructions with shared `PaymentInstructionsPanelComponent`

### Client dashboard (spec 06)

- Three sections: upcoming, pending (payment / approval), past (paginated load-more)
- Shared payment panel for upload + countdown
- Upcoming cards: voice-call instructions, WhatsApp chat link, cancellation request UX
- Arabic labels for past cancelled bookings (reason + refund)

### Admin dashboard (spec 07)

- **Settings** — consultant pricing, session duration, payment numbers
- **Availability** — recurring windows + blocked date ranges
- **Bookings** — filters, pagination, receipt review, cancellation queue, direct cancel, refund record/revoke
- **Earnings** — gross / refunded / net summary with date filters

**Not yet wired:** `GET /api/v1/admin/ops/alerts` (spec 08) — backend API exists; no admin UI page yet.

Admin HTTP services live under `src/app/core/admin/`.

## Build & test

```bash
npm run build
npm test
```

## Code scaffolding

```bash
ng generate component component-name
```

See [Angular CLI documentation](https://angular.dev/tools/cli) for more commands.

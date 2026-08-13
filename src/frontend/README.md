# Shora Web (Angular 21)

RTL frontend for the Shora personal practice booking site. API contracts live in `src/contracts/`; HTTP calls use `@contracts/*` types aligned with `Shora.Contracts`.

## Development server

```bash
npm install
npm start
```

Open `http://localhost:4200/`. The dev server proxies `/api` to the backend (`proxy.conf.json`) so auth cookies work on same-origin `localhost:4200`.

## Routes and features

| Area | Route(s) |
| ---- | -------- |
| Auth | `/auth/*` |
| Public pages | `/`, `/about`, `/services`, `/privacy`, `/terms` |
| Booking flow | `/booking/start` → delivery → phone → review → `/booking/payment/:id` |
| Client dashboard | `/dashboard` |
| Admin dashboard | `/admin/settings`, `/admin/availability`, `/admin/bookings`, `/admin/earnings`, `/admin/ops` |

### Booking flow

- Slot picker, delivery method, contact phone (voice call), review & reserve
- Post-reserve payment instructions with shared `PaymentInstructionsPanelComponent`

### Client dashboard

- Three sections: upcoming, pending (payment / approval), past (paginated load-more)
- Shared payment panel for upload + countdown
- Upcoming cards: voice-call instructions, WhatsApp chat link, cancellation request UX
- Localized labels for past cancelled bookings (reason + refund)

### Admin dashboard

- **Settings** — session pricing, duration, payment numbers
- **Availability** — recurring windows + blocked date ranges
- **Bookings** — filters, pagination, receipt review, cancellation queue, direct cancel, record refund
- **Earnings** — gross / refunded / net summary with date filters
- **Ops alerts** — active operational alerts with expandable runbook steps (`GET /admin/ops/alerts`, `GET /admin/ops/runbooks`)

Admin HTTP services live under `src/app/core/admin/`.

## Static assets (`public/`)

Ships `logo.svg` for the favicon and home page hero only. **`robots.txt` and `sitemap.xml` are not used** — traffic is expected from direct/social links, not search indexing. Add them under `public/` later if you want SEO.

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

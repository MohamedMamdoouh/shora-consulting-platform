# Shora — Application Pages Inventory

> **Purpose:** Reference document for system design and architecture work. Describes every user-facing page in the Shora frontend, what each page contains, and how pages relate to roles and workflows.

---

## 1. Application Overview

| Attribute            | Value                                                                                                                         |
| -------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| **Product name**     | Shora (شورى)                                                                                                                  |
| **Domain**           | RTL personal practice booking site for relationship consulting                                                                |
| **Primary language** | Arabic (`lang="ar"`, `dir="rtl"`)                                                                                             |
| **Target users**     | **Clients** (people booking sessions), **Admin** (single practitioner operating the practice)                                 |
| **Frontend stack**   | Angular 21, standalone components, lazy-loaded routes                                                                         |
| **Layout shell**     | All routes render inside `ShellComponent` (global header, footer, nav) except admin sub-pages which add `AdminShellComponent` |
| **API base**         | `/api/v1` (same origin in production)                                                                                         |

### Core business flow

1. Client browses marketing pages and available slots (no login required).
2. Client picks slot → delivery method → phone (if voice) → review → reserves booking (login + verified email required at reserve).
3. Client pays manually (Vodafone Cash or InstaPay), uploads receipt image.
4. Admin reviews receipt, approves/declines, manages cancellations and refunds.
5. Session delivered via voice call or WhatsApp chat at scheduled time.

### User roles and route access

| Role          | Access                                                      |
| ------------- | ----------------------------------------------------------- |
| **Anonymous** | Public pages, booking flow until review/reserve, auth pages |
| **Client**    | Client dashboard, booking payment page, own bookings        |
| **Admin**     | All `/admin/*` pages; redirected away from `/dashboard`     |

### Global shell (`ShellComponent`)

Wraps every page and provides:

- **Header navigation:** Home, About, Services, Book Session CTA, Login/Signup (or Dashboard/Admin + logout when authenticated)
- **Email verification banner:** Shown to unverified clients across all pages
- **Footer:** Brand tagline, Privacy/Terms links, copyright
- **Mobile:** Collapsible hamburger menu

---

## 2. Public Marketing Pages

### 2.1 Home — `/`

| Field       | Detail                                                   |
| ----------- | -------------------------------------------------------- |
| **Access**  | Public                                                   |
| **Purpose** | Primary landing page; convert visitors into booking flow |

**Contains:**

- **Hero section:** Brand logo, eyebrow text (“استشارات علاقات فردية”), headline, description, primary CTA to book a session, trust note, counseling scene illustration
- **Featured session card:** Live session price and duration from public settings API
- **Consultation topics grid:** 5 topic cards (Communication, Trust, Premarital, Dating Confidence, Long Distance) with short descriptions
- **How it works:** 4-step ordered list (book → pay & upload receipt → practitioner confirms → receive session)
- **Footer CTA banner:** Secondary conversion prompt to book

**Key actions:** Navigate to `/booking/start` via booking CTA buttons

**Data sources:** `GET /settings/public`, static copy (`APP_COPY`), static topic constants

---

### 2.2 About — `/about`

| Field       | Detail                                     |
| ----------- | ------------------------------------------ |
| **Access**  | Public                                     |
| **Purpose** | Introduce the practitioner and build trust |

**Contains:**

- **Page header:** “عني”
- **Pull quote:** Practitioner mission statement
- **Who I am:** Two paragraphs about experience, credentials, and cultural context
- **Approach:** Bulleted list of consulting principles (listening, no judgment, practical steps, etc.)
- **Privacy note:** Link to privacy policy
- **Footer CTA banner**

**Key actions:** Read content, navigate to privacy or booking

**Data sources:** Static copy only

---

### 2.3 Services — `/services`

| Field       | Detail                                                                  |
| ----------- | ----------------------------------------------------------------------- |
| **Access**  | Public                                                                  |
| **Purpose** | Explain the single session offering, topics, payment model, and privacy |

**Contains:**

- **Page header:** Title + intro (flat-price single session, no packages)
- **Featured session card:** Price, duration, delivery methods
- **Topics section:** Same 5 topics as home but with **long descriptions** and “discover more” links
- **Payment section:** Explains manual Vodafone Cash / InstaPay flow (no card payments)
- **Privacy section:** Pseudonym support, confidentiality assurance
- **Footer CTA banner**

**Key actions:** Navigate to booking

**Data sources:** `GET /settings/public`, static topics and copy

---

### 2.4 Privacy Policy — `/privacy`

| Field       | Detail                   |
| ----------- | ------------------------ |
| **Access**  | Public                   |
| **Purpose** | Legal privacy disclosure |

**Contains (static legal prose):**

- Last updated date
- Data collected (email, display name, booking details, receipts, server logs)
- How data is used (account, bookings, transactional email, session contact)
- Data sharing (infrastructure providers only, no sale)
- Data retention (bookings, receipt purge policy)
- User rights (access, correction, deletion request)
- Contact information
- Link back to home

**Key actions:** Read-only

---

### 2.5 Terms of Use — `/terms`

| Field       | Detail                             |
| ----------- | ---------------------------------- |
| **Access**  | Public                             |
| **Purpose** | Legal terms and service boundaries |

**Contains (static legal prose):**

- Last updated date
- Nature of service (not clinical/medical therapy)
- Booking and payment rules (manual transfer, receipt upload deadline, admin approval)
- Cancellation and refund policy summary
- User responsibilities
- Liability limits
- Changes to terms
- Link back to home

**Key actions:** Read-only

---

### 2.6 Error Catalog Index — `/errors`

| Field       | Detail                                                          |
| ----------- | --------------------------------------------------------------- |
| **Access**  | Public (developer/operator reference)                           |
| **Purpose** | Browse all API error codes returned as RFC 7807 Problem Details |

**Contains:**

- Page title and lead text (Arabic intro, English detail content)
- Error groups by category prefix (e.g. `auth`, `booking`, `payment`)
- Each entry links to detail page with: code, title, HTTP status

**Key actions:** Navigate to `/errors/:code`

**Data sources:** `GET /errors` (error catalog API)

---

### 2.7 Error Code Detail — `/errors/:code`

| Field       | Detail                                         |
| ----------- | ---------------------------------------------- |
| **Access**  | Public                                         |
| **Purpose** | Show full documentation for one API error code |

**Contains:**

- Error code, HTTP status, title
- English description and remediation guidance
- Problem Details `type` URI (copy-to-clipboard action)
- Link back to error index
- 404 state if code not found

**Key actions:** Copy type URI, navigate back

**Data sources:** `GET /errors/:code`

---

## 3. Authentication Pages

Base path: `/auth` (defaults redirect to `/auth/login`)

All auth pages support optional `returnUrl` query param for post-login redirect.

### 3.1 Login — `/auth/login`

| Field       | Detail                                                      |
| ----------- | ----------------------------------------------------------- |
| **Access**  | Public (redirects authenticated users via post-login logic) |
| **Purpose** | Sign in existing users                                      |

**Contains:**

- Email + password form with validation
- Submit button with loading state
- Google Sign-In button (shown only if `googleClientId` configured)
- Links to signup and forgot password
- Session expired info message (`?reason=sessionExpired`)
- Error messages for invalid credentials

**Key actions:** Login, Google sign-in, navigate to signup/forgot-password

**API:** `POST /auth/login`, `POST /auth/google`

---

### 3.2 Signup — `/auth/signup`

| Field       | Detail                    |
| ----------- | ------------------------- |
| **Access**  | Public                    |
| **Purpose** | Create new client account |

**Contains:**

- Form fields: email, password (min 8 chars), optional display name (pseudonym allowed)
- Submit with loading state
- Link to login
- Duplicate email error handling

**Key actions:** Register account (auto-login on success)

**API:** `POST /auth/signup`

---

### 3.3 Verify Email — `/auth/verify-email`

| Field       | Detail                                        |
| ----------- | --------------------------------------------- |
| **Access**  | Public (via email link with `?email=&token=`) |
| **Purpose** | Confirm email address after registration      |

**Contains:**

- Loading state while verifying
- Success message + auto-redirect to login (2s)
- Error state for invalid/expired links

**Key actions:** Automatic verification on page load

**API:** `POST /auth/verify-email`

---

### 3.4 Forgot Password — `/auth/forgot-password`

| Field       | Detail                       |
| ----------- | ---------------------------- |
| **Access**  | Public                       |
| **Purpose** | Request password reset email |

**Contains:**

- Email input form
- Success confirmation (always shown after submit — no email enumeration)
- Link back to login

**Key actions:** Request reset link

**API:** `POST /auth/forgot-password`

---

### 3.5 Reset Password — `/auth/reset-password`

| Field       | Detail                                        |
| ----------- | --------------------------------------------- |
| **Access**  | Public (via email link with `?email=&token=`) |
| **Purpose** | Set new password                              |

**Contains:**

- New password field (min 8 chars)
- Submit with loading state
- Success → redirect to login
- Error for expired/invalid token

**Key actions:** Set new password

**API:** `POST /auth/reset-password`

---

## 4. Booking Flow Pages

Base path: `/booking` (defaults redirect to `/booking/start`)

Multi-step wizard with step indicator. Flow state stored in **sessionStorage** via `BookingFlowStateService`. Guards enforce step order.

**Flow sequence:**

```
/start → /delivery → (/phone if VoiceCall) → /review → /payment/:id
```

### 4.1 Slot Picker — `/booking/start`

| Field       | Detail                               |
| ----------- | ------------------------------------ |
| **Access**  | Public                               |
| **Guard**   | None                                 |
| **Purpose** | Choose an available appointment slot |

**Contains:**

- Booking step indicator (step 1: slot)
- Intro text (no login required yet)
- Available slots grouped by local day (expandable `<details>` per day)
- Loading, empty, and error states with retry
- Each slot shown as selectable time button

**Key actions:** Select slot → navigates to `/booking/delivery`

**Data sources:** `GET /availability`

---

### 4.2 Delivery Method — `/booking/delivery`

| Field       | Detail                                           |
| ----------- | ------------------------------------------------ |
| **Access**  | Public                                           |
| **Guard**   | `bookingSlotSelectedGuard` (slot must be chosen) |
| **Purpose** | Choose how the session will be delivered         |

**Contains:**

- Step indicator (step 2: delivery)
- Selected slot summary
- Two options:
  - **Voice call** (مكالمة صوتية) → continues to phone step
  - **WhatsApp chat** (محادثة واتساب) → skips phone, goes to review
- Back link to slot picker

**Key actions:** Select delivery method

**State stored:** `deliveryMethod` in session flow

---

### 4.3 Contact Phone — `/booking/phone`

| Field       | Detail                                            |
| ----------- | ------------------------------------------------- |
| **Access**  | Public                                            |
| **Guard**   | `bookingPhoneGuard` (voice call selected)         |
| **Purpose** | Collect Egyptian mobile number for voice sessions |

**Contains:**

- Step indicator (includes phone step)
- Slot summary
- Phone input with Egyptian mobile validation (`+20` or `0` prefix, operator digits)
- Continue button
- Back link to delivery method

**Key actions:** Submit valid phone → `/booking/review`

**Validation:** Required; pattern `^(\+20|0)?1[0125]\d{8}$`

---

### 4.4 Booking Review — `/booking/review`

| Field       | Detail                                                         |
| ----------- | -------------------------------------------------------------- |
| **Access**  | Public to view; **login + verified email required to reserve** |
| **Guard**   | `bookingReviewGuard` (slot + delivery method set)              |
| **Purpose** | Confirm booking details and create reservation                 |

**Contains:**

- Step indicator (final pre-payment step)
- Summary: slot time, delivery method, phone (if voice)
- **If not logged in:** Prompt to login/signup with `returnUrl=/booking/review`
- **If logged in but unverified:** Block reserve + resend verification email action
- **If ready:** “Reserve” button
- Error handling for slot unavailable, hold cap exceeded, etc.
- Back navigation links

**Key actions:** Reserve booking → creates `PendingPayment` booking → `/booking/payment/:id`

**API:** `POST /bookings`, `POST /auth/resend-verification`

---

### 4.5 Payment Instructions — `/booking/payment/:id`

| Field       | Detail                                                    |
| ----------- | --------------------------------------------------------- |
| **Access**  | **Client only** (`clientGuard`)                           |
| **Purpose** | Show payment details and upload receipt after reservation |

**Contains:**

- Step indicator (payment step)
- **Payment instructions panel:**
  - Amount due (EGP)
  - Vodafone Cash number and InstaPay handle
  - Custom payment instructions text
  - Receipt upload deadline countdown timer
  - Payment method selector (Vodafone Cash / InstaPay)
  - Optional sender reference field
  - Receipt image file picker (JPEG/PNG)
  - Upload submit button
  - Decline reason display (if receipt was previously declined)
- Loading, error, and success (submitted) states
- Link to client dashboard after submission

**Key actions:** Upload receipt, go to dashboard

**API:** `GET /bookings/:id/payment-instructions`, `POST /payments/:bookingId/receipt`

---

## 5. Client Dashboard

### 5.1 Client Dashboard — `/dashboard`

| Field       | Detail                                                         |
| ----------- | -------------------------------------------------------------- |
| **Access**  | **Client only** (`clientGuard`; admins redirected to `/admin`) |
| **Purpose** | Central hub for managing all client bookings                   |

**Contains three sections:**

#### A. Upcoming Sessions (الجلسات القادمة)

Lists confirmed and active bookings (`Upcoming` filter). Each **upcoming booking card** shows:

- Slot date/time range
- Status badge (Confirmed, Cancellation Requested, etc.)
- Delivery method label
- **Voice call:** Phone number + call instruction for session time
- **Chat:** WhatsApp chat link (opens at session time)
- **Cancellation request:** Form to submit reason, pending state, declined banner with acknowledge action
- WhatsApp fallback contact for the practitioner

#### B. Pending Payment or Review (في انتظار الدفع أو المراجعة)

Lists bookings needing client action (`Pending` filter):

- **Pending Payment card:**
  - Embedded payment instructions panel (same as booking payment page)
  - Receipt upload
  - Cancel hold action
- **Pending Approval card:**
  - “Awaiting practitioner review” status
  - Cancel hold action

#### C. Past History (السجل السابق)

Paginated list of completed/cancelled bookings (`Past` filter):

- Slot time, status (Completed, Cancelled)
- Notes: cancellation reason, refund label
- “Load more” pagination

**Empty state:** If no bookings at all, shows CTAs to book session or view services.

**Key actions:** Upload receipt, cancel hold, request cancellation, acknowledge declined cancellation, load more past bookings, navigate to booking

**API:** `GET /bookings/mine`, `POST /bookings/:id/cancel`, `POST /bookings/:id/cancellation-requests`, receipt upload, mark cancellation seen

---

## 6. Admin Dashboard Pages

Base path: `/admin` (defaults redirect to `/admin/bookings`)

All admin pages require **Admin role** (`adminGuard`). Wrapped in `AdminShellComponent` with sub-navigation.

**Admin shell navigation tabs:** Settings | Availability | Bookings | Earnings | Ops Alerts

---

### 6.1 Admin Settings — `/admin/settings`

| Field       | Detail                                   |
| ----------- | ---------------------------------------- |
| **Access**  | Admin                                    |
| **Purpose** | Configure practitioner business settings |

**Contains editable form:**

| Setting                         | Description                                            |
| ------------------------------- | ------------------------------------------------------ |
| Session price                   | Flat fee in EGP                                        |
| Session duration                | Minutes per session                                    |
| Buffer minutes                  | Gap between sessions                                   |
| Receipt upload window           | Minutes client has to upload after booking             |
| Cancellation auto-decline hours | Hours before session when cancel requests auto-decline |
| Consultant WhatsApp number      | For chat sessions and client contact                   |
| Vodafone Cash number            | Payment destination                                    |
| InstaPay handle                 | Payment destination                                    |
| Payment instructions            | Free-text extra payment guidance                       |

**Read-only display:** Receipt retention months (system-managed)

**Key actions:** Load settings, save changes

**API:** `GET /admin/settings`, `PUT /admin/settings`

---

### 6.2 Admin Availability — `/admin/availability`

| Field       | Detail                                |
| ----------- | ------------------------------------- |
| **Access**  | Admin                                 |
| **Purpose** | Manage when clients can book sessions |

**Contains two management areas:**

#### A. Recurring Availability Windows

- List of weekly windows (day of week, start/end time, active flag)
- Create/edit form: day selector, time inputs, active toggle
- Delete window (regenerates available slots)
- Practitioner timezone label display

#### B. Blocked Date Ranges

- List of blocked periods with reason
- Add form: start/end datetime, optional reason
- Delete blocked range
- Conflict detection: shows conflicting booking IDs if block overlaps existing bookings

**Key actions:** CRUD availability windows, add/remove blocked dates

**API:** `GET/POST/PUT/DELETE /admin/availability-windows`, `GET/POST/DELETE /admin/blocked-dates`

---

### 6.3 Admin Bookings — `/admin/bookings`

| Field       | Detail                                            |
| ----------- | ------------------------------------------------- |
| **Access**  | Admin                                             |
| **Purpose** | Operate all bookings — default admin landing page |

**Contains:**

- **Filters:** Status dropdown, date range (from/to)
- **Paginated bookings table** with columns:
  - Client info, slot time, delivery method, status, payment status
  - Row notes (cancellation queue, refund due, etc.)
- **Row actions** (context-dependent):
  - Open receipt review panel
  - Open cancellation review panel
  - Direct cancel booking
  - Record refund

**Modal/side panels:**

- **Receipt review panel:** View receipt image (SAS URL), approve or decline with reason
- **Cancellation review panel:** Approve/decline client cancellation requests
- **Refund panel:** Record manual refund with reference

**Key actions:** Filter, paginate, approve/decline receipts, manage cancellations, cancel bookings, record refunds

**API:** `GET /admin/bookings`, receipt approve/decline, cancel, cancellation approve/decline, refund record

---

### 6.4 Admin Earnings — `/admin/earnings`

| Field       | Detail                           |
| ----------- | -------------------------------- |
| **Access**  | Admin                            |
| **Purpose** | Revenue summary for the practice |

**Contains:**

- Date range filters (from/to)
- Summary metrics:
  - Gross revenue (approved payments)
  - Total refunded
  - Net revenue
  - Booking counts
- Links to filtered bookings where relevant

**Key actions:** Apply date filters, view earnings summary

**API:** `GET /admin/earnings`

---

### 6.5 Admin Ops Alerts — `/admin/ops`

| Field       | Detail                                     |
| ----------- | ------------------------------------------ |
| **Access**  | Admin                                      |
| **Purpose** | Operational health monitoring and runbooks |

**Contains:**

- Alert counts by severity (critical, warning)
- Sorted alert list with:
  - Severity badge
  - Alert kind label
  - Context key-value details
  - Associated runbook (steps to resolve)
  - Action link to relevant admin page (e.g. pending receipts → bookings)
- Empty state when no alerts

**Key actions:** Review alerts, follow runbook steps, navigate to related admin pages

**API:** `GET /admin/ops/alerts`, `GET /admin/ops/runbooks`

---

## 7. Fallback Route

| Route                     | Behavior                |
| ------------------------- | ----------------------- |
| `**` (any unmatched path) | Redirects to `/` (home) |

---

## 8. Route Guards Summary

| Guard                      | Applied to                           | Rule                                                                                   |
| -------------------------- | ------------------------------------ | -------------------------------------------------------------------------------------- |
| `clientGuard`              | `/dashboard`, `/booking/payment/:id` | Must be authenticated client; admin → `/admin`; unauthenticated → login with returnUrl |
| `adminGuard`               | `/admin/*`                           | Must be authenticated admin                                                            |
| `bookingSlotSelectedGuard` | `/booking/delivery`                  | Slot selected in session flow                                                          |
| `bookingPhoneGuard`        | `/booking/phone`                     | Voice call delivery selected                                                           |
| `bookingReviewGuard`       | `/booking/review`                    | Slot + delivery method in session flow                                                 |

---

## 9. Shared UI Components (Cross-Page)

These appear on multiple pages and are relevant to system design:

| Component                           | Used on                               | Purpose                               |
| ----------------------------------- | ------------------------------------- | ------------------------------------- |
| `BrandLogoComponent`                | Shell, Home hero                      | Brand identity                        |
| `BookingCtaComponent`               | Home, banners                         | Link to `/booking/start`              |
| `FeaturedSessionCardComponent`      | Home, Services                        | Price/duration from public settings   |
| `TopicCardComponent`                | Home, Services                        | Consultation topic display            |
| `FooterCtaBannerComponent`          | Home, About, Services                 | Bottom conversion banner              |
| `PageHeaderComponent`               | About, Services                       | Standard page title block             |
| `CounselingSceneComponent`          | Home                                  | Hero illustration                     |
| `BookingStepIndicatorComponent`     | All booking steps                     | Progress indicator                    |
| `PaymentInstructionsPanelComponent` | Payment page, dashboard pending cards | Payment info + receipt upload         |
| `UpcomingBookingCardComponent`      | Dashboard                             | Confirmed booking management          |
| `PendingPaymentCardComponent`       | Dashboard                             | Payment + upload for pending bookings |
| `PendingApprovalCardComponent`      | Dashboard                             | Awaiting admin review state           |

---

## 10. Page Count Summary

| Area                      | Pages                                       |
| ------------------------- | ------------------------------------------- |
| Public marketing          | 5 (+ 2 error reference pages)               |
| Authentication            | 5                                           |
| Booking flow              | 5                                           |
| Client dashboard          | 1                                           |
| Admin dashboard           | 5                                           |
| **Total distinct routes** | **23** (+ dynamic `:id` and `:code` params) |

---

## 11. Booking Status → Page Relevance

| Status                  | Client sees on dashboard          | Admin action on bookings page      |
| ----------------------- | --------------------------------- | ---------------------------------- |
| `PendingPayment`        | Pending section — upload receipt  | View, direct cancel                |
| `PendingApproval`       | Pending section — awaiting review | Receipt review (approve/decline)   |
| `Confirmed`             | Upcoming section                  | Direct cancel, cancellation review |
| `CancellationRequested` | Upcoming — pending cancellation   | Cancellation review                |
| `Completed`             | Past history                      | —                                  |
| `Cancelled`             | Past history                      | Refund actions if applicable       |

---

## 12. External Integrations by Page

| Integration                                   | Pages affected                                                           |
| --------------------------------------------- | ------------------------------------------------------------------------ |
| **Google Sign-In**                            | Login                                                                    |
| **Vodafone Cash / InstaPay** (manual, no API) | Payment instructions, dashboard pending cards                            |
| **WhatsApp deep links**                       | Dashboard upcoming cards (chat delivery)                                 |
| **Azure Blob Storage** (receipt upload)       | Payment page, dashboard, admin receipt review                            |
| **Brevo email** (backend)                     | Triggered by auth, booking, payment workflows — no dedicated email pages |

---

_Generated from frontend route definitions and page components in `src/frontend/src/app/`._

# 02 — Authentication (Client + Admin)

Status: **Spec only — not implemented until explicitly requested.**

## 1. Approach

- **ASP.NET Core Identity** (as confirmed by stakeholder) manages users, roles, password hashing, external-login linking, and email confirmation.
- **Sign-in methods**: (a) **email + password**, and (b) **Google** (external login). No phone-based signup and no phone/OTP verification.
- **Email verification is required before booking** (confirmed): unverified accounts can log in and browse, but cannot reserve a slot (enforced at `POST /api/bookings`, spec 04). Google accounts are auto-verified.
- **Token type (confirmed)**: JWT bearer access tokens issued by the API, consumed by the Angular SPA via an HTTP interceptor.
- **Token expiry/refresh strategy (confirmed)**: short-lived access token + refresh token. Defaults: 15-minute access token, 7-day refresh token (adjustable).
- **Token storage (M4)**: the **access token lives in browser memory only** (never in `localStorage`, to limit XSS token theft). The **refresh token is delivered and stored in an `httpOnly`, `Secure`, `SameSite=Strict` cookie** so JavaScript cannot read it; the browser sends it automatically to `POST /api/auth/refresh` and `POST /api/auth/logout`. Refresh tokens are **persisted server-side (hashed) and rotated** with reuse-detection — see §11.
- **Deployment constraint for this cookie model (resolved):** MVP runs with frontend + API on the **same site** (same registrable domain over HTTPS; see spec 08). Cross-site cookie mode (`SameSite=None`) is out of MVP scope; if deployment topology changes later, this auth section must be revised together with CSRF controls.
- Two roles: `Client` and `Admin`. No public admin signup — only one admin account, seeded at deployment time (per spec 01 §5).

## 2. Client Signup

Two signup methods; both create an `ApplicationUser` with role `Client` and, on success, return the JWT access token in the body and set the refresh-token `httpOnly` cookie (§1, §11) — auto-login, to keep the flow low-friction per SDD §4.3.

### 2.1 Email + Password — `POST /api/auth/signup`
Request fields:
- `email` (string, required — **unique**; rejected if already used)
- `password` (string, required)
- `displayName` (string, optional — defaults to the email local-part if omitted; may be a pseudonym per SDD §5.7)

On success, a **verification email** is sent automatically (see §7). The user is logged in immediately but cannot book until verified.

### 2.2 Google — `POST /api/auth/google`
See §4 (same endpoint handles both first-time signup and returning login).

No phone is collected at signup (per spec 01 §4). A contact phone is captured later, per-booking, only for voice-call sessions (spec 04).

## 3. Login

**Endpoint**: `POST /api/auth/login`

Request fields: `email` + `password`.
Returns: JWT access token + basic profile (`displayName`, `role`) in the body; the refresh token is set as an `httpOnly` cookie (§1, §11).

**Endpoint**: `POST /api/auth/refresh` — takes **no body**; reads the refresh token from the `httpOnly` cookie. Returns a new access token and sets a **rotated** refresh-token cookie (old token invalidated) when the access token expires, without requiring the client to log in again. Reuse of an already-rotated token triggers the reuse-detection response in §11.

**Endpoint**: `POST /api/auth/logout` — revokes the current refresh token server-side and clears the cookie.

## 4. Google Sign-In (signup + login)

**Endpoint**: `POST /api/auth/google` — body: `{ idToken }`.

Flow (best fit for an Angular SPA + JWT):
1. The Angular app uses Google Identity Services to obtain a Google **ID token** client-side.
2. It posts the ID token to `POST /api/auth/google`.
3. The backend validates the ID token against Google (audience = `Google:ClientId`, see spec 01 config), extracts the verified email + name.
4. If no user exists for that email, a `Client` `ApplicationUser` is created (`DisplayName` defaulted from the Google name) with `EmailConfirmed = true` (Google has already verified the address — no verification email needed) and a Google external login is linked (`AspNetUserLogins`). If the user exists, the Google login is linked if not already.
   - **Pre-takeover guard (security):** if the existing account has `EmailConfirmed = false`, it may have been created by someone else squatting on this email (signup requires no proof of ownership until verification). Google's verified ID token *is* proof of ownership, so on link: set `EmailConfirmed = true`, **invalidate the existing password** (the account holder can set a new one via the password-reset flow), and **revoke all active refresh tokens** for that user (§11). This ensures whoever created the unverified account cannot retain access to what is now the Google user's account. Accounts that are already verified link normally with no credential changes.
5. Returns the same JWT access token + profile in the body and sets the refresh-token `httpOnly` cookie, as the other methods.

## 5. Admin Login

Same `/api/auth/login` endpoint (email + password) — role is determined by the seeded Admin account. The Angular app routes to the Admin Dashboard vs Client Dashboard based on the `role` claim in the returned token.

## 6. Password Reset (email-based reset link)

Applies to email/password accounts. (Accounts created purely via Google sign in with no password use Google to authenticate; if such a user wants a password they can use the reset flow to set one, since every account has an email on file.)

- **Endpoint**: `POST /api/auth/forgot-password` — body: `{ email }`. If a matching account exists, generates an Identity password-reset token and emails a reset link (e.g. `https://.../reset-password?token=...&email=...`). Always returns a generic success response regardless of whether the email exists (avoids leaking account existence).
- **Endpoint**: `POST /api/auth/reset-password` — body: `{ email, token, newPassword }`. Validates the token via Identity and updates the password.
- **Dependency**: requires the email-sending mechanism (`Email:*`, spec 01) to be configured. Since email is now required and unique for every account (spec 01 §4), the reset path is always available.

## 7. Email Verification (required before booking — confirmed)

Uses Identity's built-in email-confirmation token and `EmailConfirmed` flag (spec 01 §4).

- **On email/password signup:** a verification email with a confirmation link is sent automatically.
- **Endpoint**: `POST /api/auth/verify-email` — body: `{ email, token }`. Validates the Identity confirmation token and sets `EmailConfirmed = true`.
- **Endpoint**: `POST /api/auth/resend-verification` — body: `{ email }` (or inferred from the authenticated user). Re-sends the verification email; responds generically to avoid leaking account existence.
- **Enforcement:** `POST /api/bookings` rejects unverified accounts (spec 04). The booking UI shows an "verify your email first" prompt with a resend button instead of the reserve action.
- **Google accounts** skip all of this — created with `EmailConfirmed = true` (§4).
- Delivered via the same `Email:*` provider as password reset and notifications.

## 8. Authorization Rules

- Client-facing endpoints (booking, client dashboard): `[Authorize(Roles = "Client")]`.
- Admin endpoints (availability, pricing, bookings management, earnings): `[Authorize(Roles = "Admin")]`.
- Public endpoints (Home/About/Services content, published availability for browsing): no auth required.

## 9. Angular Auth Integration

- `AuthService` keeps the access token **in memory only** (never `localStorage`); the refresh token is the `httpOnly` cookie the browser holds, not JS-accessible. It exposes `currentUser$` (including verification state) and silently calls `/api/auth/refresh` (with `withCredentials: true`) when the access token expires or on app bootstrap to restore a session.
- A **"Continue with Google"** button (Google Identity Services) on the login/signup screens obtains the Google ID token and calls `POST /api/auth/google` (§4).
- `authInterceptor` attaches `Authorization: Bearer <token>` to API requests and sets `withCredentials: true` on auth calls so the refresh cookie is sent. On a 401 it attempts a single silent refresh, then retries or redirects to login.
- `logout()` calls `POST /api/auth/logout` to revoke server-side and drops the in-memory access token.
- `authGuard` / `adminGuard` route guards protect `client-dashboard` and `admin-dashboard` routes respectively.

## 10. Explicitly Out of Scope (general MVP simplicity)
- Social login providers other than Google (e.g. Facebook, Apple)
- Multi-factor authentication
- Phone/SMS verification

## 11. Refresh Token Persistence, Rotation & Reuse Detection (H2)

Refresh tokens are **not** self-contained JWTs; they are opaque random strings persisted server-side so they can be rotated and revoked.

### `RefreshToken` entity (spec 01 migration)
| Field | Type | Notes |
|---|---|---|
| Id | `Guid` | |
| UserId | `Guid` | FK to `ApplicationUser` |
| TokenHash | `string` | **SHA-256 hash** of the token; the raw token is only ever in the cookie, never stored |
| ExpiresAt | `DateTime` | UTC (7-day default) |
| CreatedAt | `DateTime` | UTC |
| RevokedAt | `DateTime?` | UTC; set on rotation, logout, or reuse-detection cascade |
| ReplacedByTokenHash | `string?` | Hash of the successor token created at rotation — forms the rotation chain used for reuse detection |
| CreatedByIp / UserAgent | `string?` | Optional, for audit |

### Rules
- **Rotation:** every successful `POST /api/auth/refresh` marks the presented token `RevokedAt = now`, sets `ReplacedByTokenHash`, and issues a brand-new refresh token (new cookie). A refresh token is single-use (subject to the grace window below).
- **Concurrent-refresh grace window (multi-tab safety):** two open tabs can race to refresh with the same cookie; without mitigation the loser looks like token theft and would nuke the session. Therefore: if a token that was rotated **within the last 60 seconds** is presented again, the server does **not** treat it as theft — it issues a fresh successor linked to the same chain (an accepted, bounded fork; both tabs end up with valid cookies). Refresh handling is serialized per token row (row lock) so the race resolves deterministically.
- **Reuse detection:** if a token revoked **more than 60 seconds ago** (or revoked by logout/cascade rather than rotation) is presented, treat it as theft — **revoke the entire rotation chain for that user** (all active refresh tokens), forcing re-login. Log a security event (spec 08). The grace window delays detection by at most ~60s while eliminating false-positive logouts for legitimate multi-tab users.
- **Expiry:** expired tokens are rejected; a periodic job (spec 08) purges rows past `ExpiresAt`.
- **Logout:** `POST /api/auth/logout` revokes the current token and clears the cookie.
- **Lookups** are by `TokenHash` (indexed); the raw token is never compared in plaintext.
- **CSRF hardening for cookie-bearing auth endpoints:** `POST /api/auth/refresh` and `POST /api/auth/logout` also validate `Origin`/`Referer` against the allowed frontend origin list (spec 08 CORS) before accepting cookie-authenticated requests.

## 12. Rate Limiting (H2)

Auth endpoints are abuse targets, so they are rate-limited (full policy + shared infrastructure in spec 08 §1). Applied here:
- `POST /api/auth/login`, `/signup`, `/google`, `/forgot-password`, `/reset-password`, `/verify-email`, `/resend-verification`: limited **per IP** and **per account/email** (e.g. a small number of attempts per minute with exponential backoff / temporary lockout on repeated failures).
- `POST /api/auth/refresh`: limited per IP to blunt brute-force of stolen cookies.
- Responses use HTTP `429 Too Many Requests` with a `Retry-After` header.
- Generic responses (no account-existence leak) still apply on the throttled paths (`forgot-password`, `resend-verification`).

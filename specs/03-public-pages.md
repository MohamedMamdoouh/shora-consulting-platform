# 03 — Public Pages (Home, About, Services)

Status: **Spec only — not implemented until explicitly requested.**

## 1. Goals

Per SDD §4 (Guiding Principles) and §5.1: these pages must feel **warm, not clinical**, load fast on mobile (primary traffic is social media), and get an already-convinced visitor into the booking flow with minimal friction. No blog/testimonials/FAQ in this phase (per SDD §9).

**Low-friction entry:** every "Book a Session" CTA leads straight into the booking flow where the visitor can browse availability and make their selections **without logging in first**; login/signup is only requested at the reserve step (see spec 04 §2). CTAs must never route to a login wall.

## 2. Home Page (`/`)

Sections (Arabic, RTL, placeholder copy until real content is provided):
1. **Hero** — warm one-line value proposition + a single prominent "Book a Session" call-to-action button (routes to `/booking/start`, which opens slot selection directly — no login required to browse and select, per spec 04 §2).
2. **What we help with** — short list of the five topics (communication, trust, premarital, dating confidence, long-distance), each a small card, purely descriptive (not clickable service tiers — pricing is flat, per SDD §5.4).
3. **How it works** — visual steps: Book a time → Transfer the fee (Vodafone Cash or InstaPay) and upload your receipt → Consultant confirms → Get called/messaged at your slot. Sets expectations that payment is by manual transfer and that delivery happens via phone/WhatsApp (per SDD §1).
4. **Secondary CTA** — repeat booking button at the bottom for mobile scroll-through visitors.

## 3. About Page (`/about`)

- Consultant bio/credentials section — **placeholder Arabic text** (e.g. generic "About the Consultant" placeholder paragraph), clearly marked in code comments as `TODO: replace with real bio content`.
- Tone: personal, warm, first-person voice — reinforces trust before booking.
- Single CTA at the end linking to booking.

## 4. Services Page (`/services`)

- Explains the five topics in a bit more depth than the Home page cards (placeholder descriptions per topic).
- States clearly: single session, flat price (rendered dynamically from `GET /api/settings/public` — price should not be hardcoded in the frontend since admin can change it), delivered via voice call or chat. Payment is by manual transfer (Vodafone Cash or InstaPay) with a receipt uploaded for the consultant to confirm — there is no online card payment.
- Explains privacy: clients can use a display name, and their information is kept confidential (reinforces SDD §5.7 to reduce stigma/privacy hesitation).
- CTA to start booking — opens the booking flow at slot selection; the visitor only logs in/signs up at the reserve step (spec 04 §2), not before browsing.

## 5. Shared Public API

**Endpoint**: `GET /api/settings/public` (no auth) — returns `{ sessionPrice, sessionDurationMinutes }` so the Home/Services pages and booking flow always reflect the current admin-configured price/duration without redeploying the frontend.

## 6. Non-Functional Notes (from SDD §6)
- Mobile-first responsive layout; test primarily at mobile viewport widths first.
- Fast initial load — keep these pages statically renderable where possible (Angular route-level code splitting so `booking`/`admin-dashboard` bundles aren't loaded on first paint).
- Full RTL layout (`dir="rtl"`), Arabic typography.

## 7. Open Items for This Area
- Actual bio/credentials content, and final Arabic copy for all placeholder text — to be supplied by the consultant later (per SDD assumption, placeholder content is acceptable for now).
- Any branding assets (logo, color palette, photo) not yet provided — placeholder visual style will be used until supplied.

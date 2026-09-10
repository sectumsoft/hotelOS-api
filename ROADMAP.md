# HotelOS — Roadmap / Task List

Backlog of everything that could still be built, grouped by area. Ticked items
are **done**; the rest are open. See `PROJECT.md` for architecture.

**Effort:** `S` = ~½–1 day · `M` = ~2–4 days · `L` = ~1–2 weeks · `XL` = 3 weeks+
**Touches:** the main files/entities involved, so you can gauge blast radius.

---

## ✅ Already shipped (for context)

- [x] Multi-tenant auth, JWT, roles (SuperAdmin / HotelAdmin / Staff)
- [x] SuperAdmin hotel onboarding (creates tenant + first admin + default room types)
- [x] Staff module-access permissions (sidebar + route guards + server-side `[ModuleAccess]` filter)
- [x] Rooms CRUD, images, amenities
- [x] Dynamic per-hotel room types (Settings → Room Types)
- [x] Bulk room import from Excel (client-parsed, `POST /api/rooms/bulk`)
- [x] Bookings: create / edit (Confirmed only) / cancel, overlap prevention
- [x] Check-in: multi-guest KYC + ID-proof upload
- [x] Check-out, room-status lifecycle (Occupied only at check-in)
- [x] Bill generation + bill view (extra services, discount, tax)
- [x] Guests list + **Guest 360** (profile, stats, stay history, check-in companions + ID-proof preview/lightbox)
- [x] Case-insensitive search across guests / rooms / bookings
- [x] Reports: date-filtered booking report + CSV export
- [x] Settings: editable hotel profile (seeded from tenant), room-type management, password change
- [x] Activity notifications (event-recorded: room/booking/check-in/check-out/cancel/bill/staff) + header bell
- [x] Global header search (rooms + bookings + guests)
- [x] Light / dark theme (no-flash init, professional light palette)
- [x] Country-code phone input (booking + settings)
- [x] Mobile responsiveness pass (layout, filters, import)
- [x] `₹` currency everywhere (`en-IN` locale)
- [x] QA hosting: Cloudflare Pages + Render (Docker) + Supabase Postgres

---

## 🎯 Priority 1 — next sprint ("make it a real product")

- [ ] **Payments ledger** — `M` — record cash/card/UPI payments against a booking, running balance, receipt
  - Touches: `Payment` entity (exists, unused), new `PaymentsController` + `PaymentsCommand`s, booking detail UI panel
  - Sub-tasks:
    - [ ] `POST /api/bookings/{id}/payments` (amount, method, note, date)
    - [ ] `GET /api/bookings/{id}/payments`
    - [ ] Recompute `Booking.BalanceAmount` from payments (single source of truth)
    - [ ] Payments panel in `bookings-list` check-in / detail modal
    - [ ] Notification on payment recorded
- [ ] **Real dashboard charts** — `M` — replace the random/hardcoded stubs
  - Touches: `DashboardController` (`revenue`, `occupancy`, `booking-sources` are fake), `GetDashboardStatsQuery`
  - Sub-tasks:
    - [ ] Revenue-by-day from actual bills/payments
    - [ ] Occupancy-by-day from bookings vs room count
    - [ ] Booking-sources from a real `Booking.Source` field (see Bookings section)
- [ ] **Object storage for uploads** — `M` — move ID proofs + room images off Render's ephemeral disk
  - Touches: `LocalImageService` → new `R2ImageService` / `S3ImageService`, `IImageService` interface stays
  - Cloudflare R2 (S3-compatible) is cheapest; needs a bucket + keys as env vars
- [ ] **Email provider** — `M` — unlocks forgot-password, bill delivery, daily reports
  - Touches: new `IEmailSender` + Resend/SendGrid impl, env-var API key
  - Sub-tasks:
    - [ ] `IEmailSender.SendAsync(to, subject, html)`
    - [ ] Forgot-password reset flow (`POST /auth/forgot`, `POST /auth/reset`)
    - [ ] Email the bill from the bill view
- [ ] **Force password change on first login** — `S`
  - Touches: `User.MustChangePassword` flag (new column + migration), login response, a `/change-password` gate
  - Onboarding UI already says "change on first login"

---

## 💰 Payments & Billing

- [ ] Payments ledger (see Priority 1)
- [ ] **Partial payments / installments** — `S` — extends the ledger
- [ ] **Refunds** on cancellation — `S` — record a negative payment; today cancel just flips status
- [ ] **Bill PDF / print view** — `M`
  - Touches: a print-styled `/bookings/{id}/bill/print` route, or server-side PDF (QuestPDF)
- [ ] **Email / WhatsApp the bill** to the guest — `M` — needs email/SMS provider
- [ ] **Tax / GST config per hotel** — `S`
  - Touches: `HotelSettings` (add `TaxPercent`, `Gstin`), bill modal (tax % is hardcoded now), bill layout
- [ ] **Discounts / coupon codes** — `M`
  - Touches: new `Coupon` entity, booking + bill flow
- [ ] **Deposit / advance policy per room type** — `S`
  - Touches: `RoomType` entity (add `MinAdvancePercent`), booking form default
- [ ] **Folio / itemised charges** during stay (not just at bill time) — `M`
  - Touches: new `FolioItem` entity linked to booking
- [ ] **Multiple currencies** (if serving non-India hotels) — `M`

---

## 🛏 Bookings & Availability

- [ ] **Date-aware room picker** — `M` — only show rooms free for the selected dates
  - Touches: `GetRoomsQuery` / a new `GET /api/rooms/available?from&to`, `booking-form` room dropdown
  - Overlap check already exists in `CreateBookingCommand` — reuse the predicate
- [ ] **Calendar → click a free day to start a booking** — `S`
  - Touches: `availability-calendar.component` (already renders free/booked per date)
- [ ] **Booking source field** (Direct / Walk-in / OTA / Travel Agent / Phone) — `S`
  - Touches: `Booking.Source` column + migration, booking form, feeds the real sources chart
- [ ] **Modify a CheckedIn booking** (extend stay, change room, add nights) — `M`
  - Touches: `UpdateBookingCommand` (Confirmed-only today), room-status implications
- [ ] **No-show handling** — `S` — mark no-show, auto-release room, optional charge
- [ ] **Group bookings** (many rooms, one guest, one folio) — `L`
  - Touches: `BookingGroup` entity, most of the booking UI
- [ ] **Rate plans / seasonal pricing** — `M`
  - Touches: new `RatePlan` entity (room type + date range + price), booking price calc
- [ ] **Housekeeping status** (Dirty / Cleaning / Inspected / Ready) — `M`
  - Touches: `Room` (add `Housekeeping` enum), a housekeeping board view, set to Dirty on check-out
- [ ] **Booking calendar / Gantt view** (rooms × dates grid) — `M`
- [ ] **Overbooking guard / waitlist** — `M`
- [ ] **Cancellation policy + cancellation fee** — `S`

---

## 👤 Guests

- [ ] **Guest profile edit** — `S` — Guest 360 is read-only today
  - Touches: `GuestsController` (add `PUT /api/guests/{id}`), Guest 360 modal
- [ ] **Merge duplicate guests** — `M` — same person created under different phone spellings
- [ ] **Guest notes / tags / VIP flag / preferences** — `S`
  - Touches: `Guest` entity (add `Notes`, `Tags`, `IsVip`), Guest 360
- [ ] **Blacklist / do-not-rent** flag — `S` — warn at booking creation
- [ ] **Loyalty points / repeat-guest discount** — `M`
  - `Guest.TotalStays` already tracked; add points ledger + redemption
- [ ] **Public self check-in link** — `L` — guest fills KYC + uploads ID before arrival via a tokenised URL
- [ ] **Guest export** (CSV/Excel) — `S`
- [ ] **ID-proof OCR** (auto-fill name/number from the uploaded image) — `L`

---

## 📊 Reports & Analytics

- [ ] Real revenue & occupancy charts (see Priority 1)
- [ ] **Occupancy % / ADR / RevPAR** KPI tiles — `M` — standard hotel metrics
- [ ] **Arrivals & departures report** (today / date range) — `S`
  - Notification logic already computes arrivals/departures/overdue — reuse
- [ ] **Outstanding balances report** — `S`
- [ ] **Revenue by room type / source / month** — `S`
- [ ] **Real `.xlsx` export** (not CSV-with-Excel-MIME) — `S` — `xlsx` lib already bundled on the frontend
- [ ] **Daily summary email** (occupancy, arrivals, revenue) — `M` — needs email + a scheduled job
- [ ] **Guest demographics / repeat-rate** — `S`
- [ ] **Custom date-range dashboard** (not just "last 30 days") — `S`

---

## 🧹 Operations

- [ ] **Audit log** (who changed what, when) — `S`
  - `NotificationRecorder` is ~80% of this; add an admin-only `/audit` view + more event types
- [ ] **Expenses / petty-cash tracking** — `M` — new `Expense` entity, category, receipt
- [ ] **Shift handover report** for front desk — `S`
- [ ] **Maintenance tickets** (raise, assign, resolve; ties to room status) — `M`
- [ ] **Services / minibar catalog** (reusable priced items) — `M`
  - Bill "extra services" are free-text today — pull from a catalog instead
- [ ] **Task checklist per booking** (welcome kit, wake-up call, airport pickup) — `S`
- [ ] **Room-move / transfer** mid-stay — `S`

---

## 🔐 Auth & Platform (SuperAdmin)

- [ ] Forgot-password reset (see Priority 1)
- [ ] Force password change on first login (see Priority 1)
- [ ] **Staff last-login + activity** — `S` — `User.LastLoginAt` column
- [ ] **2FA (TOTP)** — `M`
- [ ] **SuperAdmin: hotel usage dashboard** (rooms, bookings, staff, storage per hotel) — `M`
- [ ] **SuperAdmin: suspend / reactivate a hotel** — `S` — `Tenant.IsActive` exists but isn't enforced on login
- [ ] **Plan limits enforcement** (Basic/Pro/Enterprise → max rooms / staff / bookings) — `M`
  - `TenantPlan` enum exists; nothing checks it
- [ ] **SuperAdmin: impersonate a hotel** for support — `S`
- [ ] **Per-hotel branding** (logo, accent colour) — `M`
  - `Tenant.LogoUrl` field exists, unused; theme uses CSS vars so an accent override is easy
- [ ] **Custom subdomain routing** (`grandpalace.hotelos.com` → that tenant) — `L`
  - `Tenant.Subdomain` exists; needs wildcard DNS + host-based tenant resolution
- [ ] **SuperAdmin: edit / delete a hotel, reset an admin password** — `S`
- [ ] **Data export / tenant offboarding** (GDPR-style) — `M`

---

## 🔌 Integrations

- [ ] **Email** (Resend / SendGrid) — `M` — see Priority 1
- [ ] **SMS / WhatsApp** (Twilio / Gupshup) — `M` — booking confirmation, OTP, check-in reminder
- [ ] **Payment gateway** (Razorpay / Stripe) — `L` — online advance payment, payment links
- [ ] **iCal / Google Calendar** feed of bookings — `S`
- [ ] **OTA channel manager** (Booking.com / MakeMyTrip / Airbnb sync) — `XL`
- [ ] **Accounting export** (Tally / Zoho Books) — `M`
- [ ] **GST e-invoice** (India IRP) — `M`
- [ ] **Webhooks** (fire on booking created / checked-in for external systems) — `M`

---

## 🧪 Tech debt / polish (low risk, raises quality)

- [ ] **Remove mock-data fallbacks** in list components — `S`
  - `rooms-list`, `bookings-list`, `dashboard`, `booking-form` silently render fake data on API error → misleading in prod
- [ ] **Real `.xlsx` export** instead of CSV with Excel MIME — `S` (`ReportsController.ExportExcel`)
- [ ] **Fix `Guest.BookingId1` shadow-FK** EF warning — `S` — configure the relationship explicitly + a migration to drop the vestigial column
- [ ] **`GetById` for bookings pulls 1000 rows** then filters in memory — `S` — add a proper `GetBookingByIdQuery`
- [ ] **Pagination total re-fetched on every page nav** — `S` — cache it per filter
- [ ] **Lazy-load `xlsx`** only when the import/export modal opens — `S` — trims the rooms/reports chunk (~400 KB)
- [ ] **`AutoMapper` 13.0.1** flagged `NU1903` — `S` — bump to a patched version
- [ ] **`Npgsql.EnableLegacyTimestampBehavior`** is on — `M` — eventually normalise all `DateTime`s to UTC and remove the switch (snapshot has `timestamp` vs DB `timestamptz` drift)
- [ ] **CI**: add a GitHub Actions build/test gate before Cloudflare/Render deploy — `S`
- [ ] **Automated tests** — none exist — `L` — start with booking overlap, bill math, permissions
- [ ] **`bin/obj/.vs` were untracked from git** — confirm `.gitignore` is holding — `S`
- [ ] **Rotate the Neon DB password + JWT secret** that were once committed in `appsettings.json` history — `S`
- [ ] **Error boundary / global HTTP error toast** on the frontend — `S`
- [ ] **Loading skeletons** consistency across pages — `S`

---

## ⚠️ Known constraints (not tasks, but plan around them)

- **Render free tier**: API sleeps after 15 min idle (~50 s cold start); **no persistent disk** → uploaded files are lost on redeploy. Object storage (Priority 1) fixes the second; a paid instance fixes the first.
- **Supabase free**: pauses after ~1 week inactivity, 500 MB.
- **Staff permission changes require re-login** (baked into the JWT at login).
- **No email/SMS wired** — anything that notifies a guest is blocked until a provider is added.
- **Bill "extra services"** and **tax %** are entered free-hand each time — no saved config.

# HotelOS — Project Documentation

Multi-tenant hotel management SaaS. A platform owner (SuperAdmin) onboards hotels;
each hotel's admin manages rooms, bookings, guests, billing and staff, fully
isolated from other hotels.

- **Frontend:** Angular 17 (standalone components) → static SPA
- **Backend:** .NET 8 Web API, clean architecture, MediatR (CQRS)
- **Database:** PostgreSQL (EF Core + Npgsql)

---

## 1. Repositories

| Part | GitHub | Active branches |
|---|---|---|
| Frontend | `github.com/sectumsoft/hotelOS-frontend` | `main` (→ prod, later), `qa` (→ QA) |
| Backend | `github.com/sectumsoft/hotelOS-api` | `main` (→ prod, later), `qa` (→ QA) |

`qa` is the integration branch that auto-deploys to the QA environment. `main` is
reserved for production (not deployed yet).

---

## 2. Roles

Defined in `HotelManagement.Domain/Enums/RoomStatus.cs`:

```csharp
public enum UserRole { SuperAdmin = 1, HotelAdmin = 2, Staff = 3 }
```

| Role | Belongs to a tenant? | Can do |
|---|---|---|
| **SuperAdmin** | No (`TenantId = null`) | List all hotels, onboard new hotels (creates tenant + first HotelAdmin), add more HotelAdmins |
| **HotelAdmin** | Yes | Everything for their own hotel: rooms, bookings, guests, reports, settings, **and** manage Staff |
| **Staff** | Yes | Day-to-day ops for their hotel: rooms, bookings, check-in/out, billing, guests, reports, settings (no user management) |

Registration is intentionally **not** self-service. The chain is:
SuperAdmin → onboards hotel + HotelAdmin → HotelAdmin creates Staff.

---

## 3. Credentials (QA — seeded automatically)

| Role | Email | Password |
|---|---|---|
| SuperAdmin | `superadmin@hotelos.com` | `superadmin123` |
| HotelAdmin | `admin@grandhotel.com` | `password123` |
| Staff | `staff@grandhotel.com` | `password123` |

Seeded by `HotelManagement.Infrastructure/Data/DbInitializer.cs` on startup:

- **On a fresh database:** creates the "Grand Hotel" tenant, its HotelAdmin + Staff, and 6 demo rooms.
- **Always:** ensures at least one SuperAdmin exists.

> Change these before anything resembling production. The SuperAdmin password is
> hard-coded in the seeder — move it to an env var or disable seeding for prod.

---

## 4. Environments

| | Local | QA |
|---|---|---|
| Frontend | `http://localhost:4200` (`npm start`) | `https://hotelos-frontend.pages.dev` (Cloudflare Pages) |
| Backend | `https://localhost:62481` (`dotnet run`) | `https://hotelos-api-qa.onrender.com` (Render, Docker) |
| Database | local Postgres (Docker) | Supabase project `hotelos-qa` (region `ap-southeast-1`) |
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Staging` |
| Cost | — | **$0/mo** (all free tiers) |

Production is not provisioned yet. Plan: Cloudflare Pages (free) + Render paid
instance or Azure App Service + managed Postgres, promoted from `main`.

---

## 5. Modules

### 5.1 Frontend (`src/app/modules/`)

| Module | Routes | Access |
|---|---|---|
| `auth` | `/auth/login` | Public |
| `dashboard` | `/dashboard` | Any signed-in user except SuperAdmin |
| `rooms` | `/rooms`, `/rooms/add`, `/rooms/edit/:id` | ” |
| `bookings` | `/bookings`, `/bookings/add`, `/bookings/edit/:id` | ” |
| `guests` | `/guests` | ” |
| `reports` | `/reports` | ” |
| `settings` | `/settings` | ” |
| `users` | `/users` | **HotelAdmin only** |
| `superadmin` | `/superadmin`, `/superadmin/onboard` | **SuperAdmin only** |
| `hotels` | (rendered at `/superadmin`) | SuperAdmin |

**Core** (`src/app/core/`)

- Guards: `authGuard` (logged-in), `roleGuard(roles[])` (allow-list, redirects to role's home on fail), `notSuperAdminGuard` (bounces SuperAdmin off tenant pages to `/superadmin`)
- Interceptor: `authInterceptor` — attaches `Authorization: Bearer <token>` and `X-Tenant-Id`; logs out on HTTP 401
- Services: `auth`, `booking`, `dashboard`, `guest`, `report`, `room`, `settings`, `theme`, `toast`

**Shared** (`src/app/shared/`)

- `layout` (authenticated shell), `sidebar` (role-driven nav), `topbar`, `toast-container`
- `models/index.ts` — all TypeScript interfaces (Booking, Room, Guest, DashboardStats, Bill, …)

### 5.2 Backend

Clean architecture, 4 projects:

| Project | Contains |
|---|---|
| `HotelManagement.API` | Controllers, `Program.cs`, middleware, DTOs |
| `HotelManagement.Application` | MediatR command/query handlers under `Features/*`, mapping profiles |
| `HotelManagement.Domain` | Entities (`Booking`, `Room`, `Guest`, `User`, `Tenant`, `Bill`, `HotelSettings`), enums |
| `HotelManagement.Infrastructure` | `ApplicationDbContext`, EF migrations, services (`JwtService`, `TenantService`, `CurrentUserService`, `LocalImageService`), `DbInitializer` |

**Controllers**

| Controller | Route | Auth | Notes |
|---|---|---|---|
| `AuthController` | `/api/auth` | anonymous | `login`, `change-password` |
| `RoomsController` | `/api/rooms` | `[Authorize]` | CRUD + `availability/date` |
| `BookingsController` | `/api/bookings` | `[Authorize]` | CRUD + `checkin`, `{id}/checkout`, `{id}/cancel`, `{id}/generate-bill`, `{id}/bill` |
| `GuestsController` | `/api/guests` | `[Authorize]` | |
| `DashboardController` | `/api/dashboard` | `[Authorize]` | `stats`, `revenue`, `occupancy`, `booking-sources` |
| `ReportsController` | `/api/reports` | `[Authorize]` | |
| `SettingsController` | `/api/settings` | `[Authorize]` | |
| `UsersController` | `/api/users` | `[Authorize(Roles="HotelAdmin")]` | list / create / delete **Staff** only, tenant locked to caller |
| `SuperAdminController` | `/api/superadmin` | `[Authorize(Roles="SuperAdmin")]` | `hotels`, `onboard`, `hotels/{id}/users`, `hotels/{id}/add-admin` |

**Multi-tenancy** — `ITenantService.TenantId` reads the `tenantId` claim from the
JWT (falls back to an `X-Tenant-Id` header). Every tenant-scoped query filters by
it, so a token can only ever see its own hotel's data. SuperAdmin tokens carry no
`tenantId` claim.

**JWT** — HS256, 24h expiry. Claims: `nameidentifier`, `email`, `name`, `role`,
and `tenantId` (only when the user has a tenant). Secret / Issuer / Audience from
config.

---

## 6. Application flow

```
Not signed in
  └─ /auth/login  → AuthService.login()
       stores token, refreshToken, user, tenant in localStorage
       → redirect to AuthService.homeRoute

Signed in (authGuard on the layout shell; authInterceptor adds the token)
  │  sidebar nav is role-driven
  │
  ├─ SuperAdmin           → lands on /superadmin
  │    nav: Hotels, Onboard Hotel
  │    Onboard → POST /api/superadmin/onboard
  │      creates a Tenant + a HotelAdmin (temp password)
  │
  ├─ HotelAdmin           → lands on /dashboard
  │    nav: Dashboard, Rooms, Bookings, Guests, Reports, Settings, Users
  │    Users → create / list / delete Staff (own hotel only)
  │    all data auto-scoped to their TenantId
  │
  └─ Staff                → lands on /dashboard
       nav: same as HotelAdmin minus Users
```

`notSuperAdminGuard` sends a SuperAdmin who hits a tenant URL (`/rooms`, …) back
to `/superadmin`; `roleGuard` sends anyone else who lacks access back to their own
home route.

---

## 7. Configuration & secrets

Secrets are **never committed**. `appsettings.json` holds only non-secret config
(`Jwt:Issuer/Audience`, `Cors:Origins`, logging) plus commented placeholders.

**Supplied as environment variables in hosted environments:**

| Variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | Postgres connection string |
| `Jwt__Secret` | JWT signing key (min 32 chars) |
| `Cors__Origins__0`, `__1`, … | allowed front-end origins |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Staging` / `Production` |
| `PORT` | injected by the host; `Program.cs` binds Kestrel to it |

(`.NET` maps `__` in an env var name to a nested config key.)

**Local:** put secrets in `HotelManagement.API/appsettings.Development.json`
(git-ignored, auto-loaded when `ASPNETCORE_ENVIRONMENT=Development`). Example:

```json
{
  "Logging": { "LogLevel": { "Default": "Debug", "Microsoft.AspNetCore": "Warning" } },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=hotelos;Username=postgres;Password=postgres"
  },
  "Jwt": { "Secret": "local-dev-secret-min-32-characters-xxxxxxxxxx" },
  "Cors": { "Origins": [ "http://localhost:4200" ] }
}
```

`Program.cs` also: enables `UseHttpsRedirection()` **only** in Development (hosted
platforms terminate TLS at their proxy), creates `wwwroot/Uploads/rooms` on start
(fresh containers have no `wwwroot`), and always exposes Swagger at `/swagger`.

**Frontend** — `apiUrl` is baked in at build time:

| Angular configuration | Uses | `apiUrl` |
|---|---|---|
| `development` (default, `ng serve`) | `environment.ts` | QA API |
| `qa` (`npm run build:qa`) | `environment.qa.ts` | `https://hotelos-api-qa.onrender.com/api` |
| `production` (`npm run build:prod`) | `environment.prod.ts` | `https://api.hotelos.com/api` |

`environment.ts` ships pointing at the QA API, with a commented `localhost:62481`
block. Uncomment it (and comment the QA block) only when running the backend
locally; re-comment before committing.

---

## 8. Local development

### One-time setup

**Database** (Docker):

```bash
docker run -d --name hotelos-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=hotelos -p 5432:5432 postgres:16
```

(or install PostgreSQL for Windows natively; it bundles pgAdmin.)

Then create `HotelManagement.API/appsettings.Development.json` as shown in §7.

**Inspect the DB** — use a Postgres client (this project is **not** SQL Server, so
SSMS won't connect): **DBeaver** or **pgAdmin 4**. Connect with
`localhost:5432`, db `hotelos`, user `postgres`, password `postgres`. For QA, use
the Supabase dashboard's Table Editor / SQL Editor.

### Run

```bash
# backend
cd backend/HotelManagement.API && dotnet run          # https://localhost:62481, Swagger at /swagger

# frontend
cd frontend && npm start                              # http://localhost:4200
```

Migrations + seed run automatically on backend start, so local gets the same
credentials as §3.

If you're only changing frontend code, skip the local backend — `npm start`
already talks to the QA API.

---

## 9. Development workflow

```bash
git checkout qa && git pull
git checkout -b feature/<name>          # in whichever repo you're changing

# edit code, test locally at http://localhost:4200

git status                             # confirm no local-only config leaked in
git add -A
git commit -m "feat: <name>"
git push -u origin feature/<name>
# open a PR  feature/<name> -> qa  on GitHub, review, merge
```

Merging to **`qa`** auto-deploys: Render rebuilds the API, Cloudflare rebuilds the
site. Verify at `https://hotelos-frontend.pages.dev`.

Promote to production later with a PR **`qa` → `main`** (prod hosting TBD).

**Rules of thumb**

- Never commit `appsettings.Development.json` or an uncommented `localhost` block in `environment.ts`.
- New npm package → commit `package.json` **and** `package-lock.json` together.
- New backend config key → `appsettings.json` (non-secret) or a Render env var (secret).
- Keep `qa` deployable; break things on feature branches.

### Database changes (only if you touch entities / `DbContext`)

```bash
dotnet tool install --global dotnet-ef      # once
cd backend
dotnet ef migrations add <Name> --project HotelManagement.Infrastructure --startup-project HotelManagement.API
```

Commit the files under `HotelManagement.Infrastructure/Migrations/`. `DbInitializer`
applies them on the next deploy (`Database.MigrateAsync()`).

---

## 10. Deployment

### Backend — Render

- Service `hotelos-api-qa`, **Docker** runtime, branch `qa`, Free instance, Singapore region.
- Build context: repo root `Dockerfile` (multi-stage: `dotnet/sdk:8.0` build → `dotnet/aspnet:8.0` runtime, layered restore, `mkdir wwwroot/Uploads/rooms`, `EXPOSE 8080`).
- Environment variables:

  ```
  ASPNETCORE_ENVIRONMENT               = Staging
  ConnectionStrings__DefaultConnection = <Supabase session-pooler string>
  Jwt__Secret                          = <random 40+ chars>
  Cors__Origins__0                     = https://hotelos-frontend.pages.dev
  ```

- Auto-deploys on push to `qa`.

### Frontend — Cloudflare Pages

- Project `hotelos-frontend`, branch `qa`.
- Build command: `npm run build:qa` — Output directory: `dist/hotel-saas-frontend/browser`
- Environment variable: `NPM_FLAGS = --legacy-peer-deps`
- Stable URL: `https://hotelos-frontend.pages.dev` (per-deployment `*.pages.dev`
  preview URLs also exist but change every build — always use the stable one).
- Auto-deploys on push to `qa`.

### Database — Supabase

- Project `hotelos-qa`, PostgreSQL, region `ap-southeast-1`.
- Backend connects via the **session pooler**
  (`aws-0-ap-southeast-1.pooler.supabase.com:5432`, db `postgres`,
  user `postgres.<project-ref>`). The password lives only in Render's env var.
- Schema is created and kept current by EF migrations on backend startup.

---

## 11. Known limitations & gotchas

| Item | Detail |
|---|---|
| Render free tier sleeps | After ~15 min idle; next request takes ~50s to wake. Not a bug. |
| Uploads are ephemeral on QA | Render free has no persistent disk — files in `wwwroot/Uploads` are lost on redeploy. Needs object storage (R2 / S3) for prod. |
| `AutoMapper` 13.0.1 | Flagged `NU1903` (known high-severity advisory). Bump when convenient. |
| `ng-apexcharts` 1.17.1 | Peers Angular 20 but the app is Angular 17 → `.npmrc` sets `legacy-peer-deps=true` so installs resolve. |
| EF warning: `Guest.BookingId1` shadow FK | Pre-existing model mapping quirk, non-fatal. Booking↔Guest relationship should be mapped explicitly at some point. |
| CORS "invalid credentials" | The login screen shows "Invalid credentials" for *any* failed request, including a CORS block. If login fails after a URL change, check `Cors__Origins__0` matches the front-end origin exactly (no trailing slash). |

---

## 12. Changes made while setting up QA

**Backend**

- `DbInitializer.cs` — always ensure a SuperAdmin; seed a demo Staff on fresh DBs.
- `JwtService.cs` — emit the `tenantId` claim only when the user has a tenant (SuperAdmin-safe).
- `Program.cs` — bind to `$PORT`; CORS origins from config/env; HTTPS redirect only in Development; ensure `wwwroot/Uploads` exists before serving static files.
- `Dockerfile` — rewritten (layered restore, explicit project, port/expose).
- `.gitignore` / `.dockerignore` — added; stopped tracking `bin/`, `obj/`, `.vs/`.
- Removed 4 stale SQL-Server migration files that broke local builds (project is Postgres-only).

**Frontend**

- `AuthService.homeRoute` + `login.component.ts` — role-based landing (SuperAdmin → `/superadmin`, others → `/dashboard`).
- `role.guard.ts` — added `notSuperAdminGuard`; `roleGuard` fallback is role-aware.
- `app.routes.ts` — `/users` restricted to HotelAdmin; `notSuperAdminGuard` on tenant routes.
- `angular.json` — added `fileReplacements` to `production` (it was missing, so `environment.prod.ts` was never applied) and a new `qa` configuration.
- `environment.ts` / `environment.qa.ts` — restructured for the local ↔ QA toggle.
- `.gitignore` (was absent), `.npmrc` (`legacy-peer-deps=true`), `src/_redirects`.
- `bookings-list.component.ts` + `models/index.ts` — `numberOfGuests` field (pre-existing local work, committed alongside).

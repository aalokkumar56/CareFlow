# Vite → Next.js migration (dotnet-frontend)

Cure-Flow is a **public-facing healthcare app** with authenticated internal routes. The frontend now runs on **Next.js 15 (App Router)** with **React 19**.

## Stack

| Layer | Choice |
|-------|--------|
| Framework | Next.js 15 App Router |
| UI | React 19, Tailwind CSS 3.4, glass design system in `src/components/glass/` |
| Icons | `@phosphor-icons/react` v2 (`*Icon` suffix where migrated) |
| API client | Axios in `src/lib/api.js` — JWT from `localStorage` |
| Unit tests | Jest via `next/jest` (`jest.config.js`) |
| E2E | Playwright on port **3000** |

## Scripts

```bash
npm run dev      # Next.js dev server :3000
npm run build    # production build → .next/
npm run start    # serve production build :3000
npm run lint     # next lint
npm test         # Jest unit tests
npm run test:e2e # Playwright (start API + dev server first)
```

## Environment variables

Copy `.env.example` to `.env.local`:

```env
NEXT_PUBLIC_BACKEND_URL=http://localhost:5180
```

- Only `NEXT_PUBLIC_*` vars are exposed to the browser.
- When `NEXT_PUBLIC_BACKEND_URL` is **unset**, the browser calls `/api` and Next.js rewrites to the backend (see `next.config.js`).
| Legacy (CRA/Vite) | Next.js |
| --- | --- |
| `REACT_APP_BACKEND_URL` | `NEXT_PUBLIC_BACKEND_URL` |
| `VITE_BACKEND_URL` | `NEXT_PUBLIC_BACKEND_URL` |
| `REACT_APP_*` (general) | `NEXT_PUBLIC_*` |
| `VITE_*` (general) | `NEXT_PUBLIC_*` |

Legacy names are no longer read by `src/lib/api.js` (uses `NEXT_PUBLIC_BACKEND_URL`).

## API proxy / rewrites

`next.config.js` rewrites `/api/*` → `{NEXT_PUBLIC_BACKEND_URL or http://localhost:5180}/api/*`.

Start the .NET API separately:

```bash
cd dotnet-backend/src/CureFlow.Api && dotnet run
```

## Routing

React Router was replaced by the App Router under `app/`. Existing page components remain in `src/pages/`.

### Public routes

| URL | File |
|-----|------|
| `/login` | `app/login/page.jsx` |

### Protected routes (client auth guard)

JWT is stored in `localStorage` (`cureflow_token`). `ProtectedRoute` + `AuthProvider` run on the client; middleware is a pass-through placeholder for future cookie-based auth.

| URL | Page component | Permission |
|-----|----------------|------------|
| `/` | Dashboard | `DashboardView` |
| `/inbox` | Inbox | `ConversationView` |
| `/email-inbox` | EmailInbox | `ConversationView` |
| `/patients` | Patients | `PatientView` |
| `/patients/[id]` | PatientDetail | `PatientView` |
| `/appointments` | Appointments | `AppointmentView` |
| `/tasks` | Tasks | `DashboardView` |
| `/doctors` | Doctors | `ReferralView` |
| `/doctors/[id]` | DoctorDetail | `ReferralView` |
| `/staff` | Staff | `StaffView` or `ClinicalView` |
| `/campaigns` | Campaigns | `CampaignView` |
| `/campaigns/[id]` | CampaignDetail | `CampaignView` |
| `/missed-revenue` | MissedRevenue | `DashboardView` |
| `/settings` | Settings | `SettingsView` |
| `/settings/users` | UsersPage | `UserView` |
| `/settings/roles` | RolesPage | `UserView` |
| `/settings/permissions` | PermissionsPage | `UserView` |
| `/settings/templates` | TemplatesPage | `SettingsView` |
| `/settings/notifications` | NotificationsPage | `SettingsView` |
| `/settings/hospital` | HospitalPage | `SettingsView` |
| `/settings/integrations` | IntegrationsPage | `SettingsView` |
| `/notifications/preferences` | NotificationPreferencesPage | (authenticated) |

Thin route files use `ClientPage` to wrap `ProtectedRoute` + `RequirePermission`.

Unknown URLs hit `app/not-found.jsx`, which redirects to `/` (same as the old React Router `*` catch-all).

### Auth guard flow

```
app/*/page.jsx  →  ClientPage  →  ProtectedRoute (JWT in localStorage)
                               →  RequirePermission (role/permission check)
                               →  src/pages/* (unchanged screen components)
```

`middleware.js` is a pass-through today; server-side redirects will land there when auth moves to cookies.

### Navigation shim

`src/lib/navigation.jsx` provides React Router–compatible `Link`, `NavLink`, `useNavigate`, `useParams`, and `useSearchParams` on top of `next/navigation` so existing components needed minimal edits.

## Project layout

```
dotnet-frontend/
  app/                 # Next.js routes (thin wrappers)
  src/
    components/        # UI + glass system (unchanged)
    pages/             # Screen components (unchanged)
    lib/               # api, auth, navigation, permissions
  public/              # static assets
  middleware.js        # placeholder for future server auth
  next.config.js
  tailwind.config.js
  vitest.config.js   # optional; Jest is the default test runner
```

## Removed

- `react-scripts`, `@craco/craco`, `vite`, `react-router-dom`
- `src/App.js`, `src/index.js`, CRA/Vite entry HTML

## SEO / public pages

The root layout exports `metadata` for CureFlow. Add marketing/landing routes under `app/(marketing)/` as server components when needed; authenticated app screens stay client-rendered.

## Deferred

- Cookie/session middleware (JWT is client-only today)
- Tailwind v4, ESLint flat config polish
- Full TypeScript migration (`app/` and `src/` remain JSX)

## Test verification (2026-07-02)

### Unit tests (Jest + `next/jest`)

| Command | Result |
|---------|--------|
| `npm test -- src/lib/globalLoader.test.js src/components/GlobalLoader.test.jsx --watchAll=false` | **6/6 passed** |

Harness notes:

- `jest.config.js` uses `next/jest` with `src/setupTests.js` (mocks UI primitives + `@phosphor-icons/react`).
- Do **not** set `pageExtensions` to `*.page.jsx` in `next.config.js` — that pattern expects filenames like `about.page.jsx`, not `app/page.jsx`, and causes 404s for every route.

### E2E (Playwright)

Prerequisites:

```bash
# Terminal 1 — API (default :5180)
cd dotnet-backend/src/CureFlow.Api && dotnet run

# Terminal 2 — optional; Playwright can start the frontend via webServer
cd dotnet-frontend && npm run dev
```

Playwright config (`playwright.config.js`):

- `baseURL`: `http://localhost:3000` (`PLAYWRIGHT_BASE_URL` override supported)
- `webServer`: `npm run dev`, readiness probe `GET /login` (200)
- `PLAYWRIGHT_SKIP_WEBSERVER=1` — use an already-running dev server
- `PLAYWRIGHT_API_URL` — direct backend URL for API login helper (default `http://localhost:5180`)
- First run: `npx playwright install chromium`

| Suite | Command | Result |
|-------|---------|--------|
| Smoke | `npm run test:e2e -- e2e/smoke.spec.js` | **7/7 passed** |
| Design (sample) | `DESIGN_SAMPLE=1 npm run test:e2e -- e2e/design/glass-surfaces.spec.js` | **54/54 passed** |

E2E harness fixes for Next App Router:

- `e2e/helpers/auth.js` — `page.reload()` after seeding `localStorage` so the root-layout `AuthProvider` re-initializes (layout persists across client navigations).
- SSR guards in `src/lib/auth.jsx` (`readStorage`) and `src/components/GlobalLoader.jsx` (`getServerSnapshot` for `useSyncExternalStore`).

### Outstanding (not in smoke scope)

- Remaining unit suites (`EmailInbox.test.jsx`, `IntegrationsPanel.test.jsx`, `WhatsAppChatPanel.test.jsx`) still use Jest globals; run with `npm test` after `npm install --legacy-peer-deps`.
- Full design matrix (without `DESIGN_SAMPLE=1`) is slower but uses the same Playwright + Next setup.
- Backend must be running for e2e; Playwright `webServer` only starts the Next dev server.

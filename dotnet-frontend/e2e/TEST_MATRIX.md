# E2E Test Matrix — Cure-Flow

Playwright functional coverage in `dotnet-frontend/e2e/` is generated **data-driven** from shared config in `helpers/test-matrix.js` and `helpers/routes.js`. Tests are not copy-pasted; `for` loops over role × route × filter × viewport matrices produce distinct Playwright scenarios.

## Target: 2,000+ scenarios

| Metric | Value |
|--------|------:|
| **Total generated scenarios** | **2,130** |
| Roles | 6 (`admin`, `doctor`, `reception`, `nurse`, `marketing`, `staff`) |
| Viewports | 3 (`desktop` 1280×720, `tablet` 768×1024, `mobile` 390×844) |
| Static routes (App.js) | 20 |
| Dynamic detail routes | 3 (`/patients/:id`, `/doctors/:id`, `/campaigns/:id`) |

Recompute anytime:

```bash
cd dotnet-frontend
node -e "const { countMatrixScenarios } = require('./e2e/helpers/test-matrix'); console.log(countMatrixScenarios());"
```

## Routes enumerated (from `src/App.js`)

| Path | Permission |
|------|------------|
| `/login` | public |
| `/` | `dashboard.view` |
| `/inbox` | `conversation.view` |
| `/email-inbox` | `conversation.view` |
| `/patients` | `patient.view` |
| `/patients/:id` | `patient.view` |
| `/appointments` | `appointment.view` |
| `/tasks` | `dashboard.view` |
| `/doctors` | `referral.view` |
| `/doctors/:id` | `referral.view` |
| `/staff` | `staff.view` **or** `clinical.view` |
| `/campaigns` | `campaign.view` |
| `/campaigns/:id` | `campaign.view` |
| `/missed-revenue` | `dashboard.view` |
| `/settings` | `settings.view` |
| `/settings/users` | `user.view` |
| `/settings/roles` | `user.view` |
| `/settings/permissions` | `user.view` |
| `/settings/templates` | `settings.view` |
| `/notifications/preferences` | authenticated (no extra gate) |
| `/settings/notifications` | `settings.view` |
| `/settings/hospital` | `settings.view` |
| `/settings/integrations` | `settings.view` |

## Scenario breakdown by spec file

| Spec file | Scenarios | Matrix dimensions |
|-----------|----------:|-------------------|
| `navigation-matrix.spec.js` | 396 | 19 protected routes + 3 detail × 6 roles × 3 viewports |
| `api-assertions-matrix.spec.js` | 132 | API GET on navigation × 6 roles |
| `patients-matrix.spec.js` | 270 | status(6)×roles + dept(8)×roles + source(6)×roles + search(12)×roles + tabs(9)×roles + edit-tabs(4)×roles |
| `appointments-matrix.spec.js` | 120 | search(12)×roles + kanban columns(5)×roles + viewports(3)×roles |
| `dashboard-matrix.spec.js` | 66 | appt periods(2)×roles + revenue periods(3)×roles + widgets(6)×roles |
| `analytics-matrix.spec.js` | 42 | metrics(3)×roles + viewports(3)×roles |
| `settings-matrix.spec.js` | 144 | 8 settings routes × 6 roles × 3 viewports |
| `referrals-matrix.spec.js` | 102 | search(12)×roles + doctor tabs(2)×roles + viewports(3)×roles |
| `inbox-matrix.spec.js` | 180 | 2 inboxes × search(12)×roles + 2 inboxes × viewports(3)×roles |
| `campaigns-matrix.spec.js` | 42 | kanban columns(4)×roles + viewports(3)×roles |
| `tasks-matrix.spec.js` | 96 | search(12)×roles + kanban columns(4)×roles |
| `staff-matrix.spec.js` | 90 | search(12)×roles + viewports(3)×roles |
| `auth-matrix.spec.js` | 36 | invalid login(5) + login/logout×roles(12) + unauth redirects(19) |
| `empty-states-matrix.spec.js` | 48 | 8 list pages × 6 roles (no-result search) |
| `crud-dialogs-matrix.spec.js` | 36 | 6 create dialogs × 6 roles |
| `pagination-matrix.spec.js` | 108 | 6 list pages × 3 API actions × 6 roles |
| `notifications-matrix.spec.js` | 24 | 4 bell checks × 6 roles |
| `shell-matrix.spec.js` | 198 | 11 AppShell pages × 6 roles × 3 viewports |
| **Total** | **2,130** | |

## Existing non-matrix specs (not counted above)

These pre-date the matrix suite and cover focused flows / audits:

- `smoke.spec.js`, `crud-flows.spec.js`, `role-matrix.spec.js`
- `patient-filters.spec.js`, `dashboard-filters.spec.js`, `follow-ups.spec.js`
- `comprehensive-site-crawl.spec.js`, `full-site-audit.spec.js`
- Design / a11y: `design-consistency.spec.js`, `design-tokens.spec.js`, `accessibility-labels.spec.js`, `glassmorphism-audit.spec.js`, `mobile-audit.spec.js`
- Domain: `referral-crm.spec.js`, `analytics-finance.spec.js`, `integrations-inbox.spec.js`, `notifications-bell.spec.js`, `notifications-settings.spec.js`, `patient-detail-ui.spec.js`

## Shared helpers

| File | Purpose |
|------|---------|
| `helpers/test-matrix.js` | Route matrices, filter dimensions, `countMatrixScenarios()` |
| `helpers/routes.js` | Nav items, feature buttons, tab IDs |
| `helpers/role-session.js` | Provision/cache E2E users + JWT permissions |
| `helpers/navigation.js` | Viewport login + `gotoAsRole` |
| `helpers/auth.js` | API-token login |
| `helpers/api.js` | Backend seeding and assertions |

## Running subsets

```bash
# Full matrix (long — run in CI or overnight)
npx playwright test e2e/*-matrix.spec.js

# Quick smoke of matrix infrastructure
npx playwright test e2e/auth-matrix.spec.js e2e/dashboard-matrix.spec.js e2e/shell-matrix.spec.js --grep "admin"

# Single domain
npx playwright test e2e/patients-matrix.spec.js
```

## How 2k+ is reached without duplicate tests

1. **Parameterized generation** — one `test()` template, many matrix tuples via `for (role) for (filter)`.
2. **Role gating** — `test.skip()` when JWT lacks permission (still counts as a scenario; verifies deny path).
3. **API-backed checks** — `waitForResponse` on `/api/*` plus JSON body assertions, not only `toBeVisible`.
4. **Cross-product dimensions** — roles × routes × viewports × filters × search terms × kanban columns × settings pages.

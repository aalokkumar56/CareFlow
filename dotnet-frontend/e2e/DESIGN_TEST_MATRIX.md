# Cure-Flow Design E2E Test Matrix

Parameterized Playwright design/UI suite under `e2e/design/`, built on `ui-audit.js`, `design-matrix.js`, and legacy specs (`design-consistency.spec.js`, `design-tokens.spec.js`, `glassmorphism-audit.spec.js`).

## Inputs

| Dimension | Count | Values |
|-----------|------:|--------|
| Static routes | 18 | `DESIGN_AUDIT_PAGES` (17) + `/login` |
| Dynamic routes | 2 | Patient detail, doctor detail (seeded in `beforeAll`) |
| **Effective routes** | **21** | Used for scenario math |
| Viewports | 3 | mobile 375×812, tablet 768×1024, desktop 1280×800 |
| **Route × viewport** | **63** | 21 × 3 |

## Spec files (`e2e/design/`)

| File | Formula | Scenarios |
|------|---------|----------:|
| `route-viewport-matrix.spec.js` | 21 routes × 3 vp × 10 dimensions | **630** |
| `screenshot-regression.spec.js` | 21 × 3 | **63** |
| `glass-surfaces.spec.js` | (21 × 3 × 3 surfaces) + (3 kanban × 3 vp × 2) | **207** |
| `typography-spacing.spec.js` | 21 routes × 3 vp × (4 typo + 3 spacing) | **441** |
| `accessibility-design.spec.js` | 21 routes × 3 vp × 5 a11y checks | **315** |
| `components.spec.js` | See breakdown below | **303** |

### Component breakdown (`components.spec.js`)

| Component | Formula | Scenarios |
|-----------|---------|----------:|
| Kanban boards | 3 boards × 3 vp × 5 checks | 45 |
| Dashboard stat cards | 4 cards × 3 vp × 3 checks | 36 |
| Data tables | 5 pages × 3 vp × 4 checks | 60 |
| Modals | 6 triggers × 3 vp × 2 checks | 36 |
| Sidebar nav | 11 items × 3 vp × 2 checks | 66 |
| Patient detail tabs | 9 tabs × 3 vp × 2 checks | 54 |
| **Subtotal** | | **303** |

## Legacy design specs (root `e2e/`)

| Spec | Approx. scenarios |
|------|------------------:|
| `design-consistency.spec.js` | 8 |
| `design-tokens.spec.js` | 8 |
| `glassmorphism-audit.spec.js` | 12 |
| `accessibility-labels.spec.js` | 19 |
| **Subtotal** | **47** |

## Combined functional + design target

| Suite | Scenarios |
|-------|----------:|
| Design matrix (`e2e/design/`) | **2,000** |
| Legacy design specs | 47 |
| Functional E2E (smoke, CRUD, role-matrix, notifications, etc.) | ~180 |
| **Grand total** | **~2,227** |

### Route × viewport dimension list (`route-viewport-matrix.spec.js`)

1. `layout-no-overflow` — no horizontal scroll
2. `page-chrome` — page-title or breadcrumb
3. `title-font` — Outfit/heading family
4. `title-color` — `rgb(2, 44, 34)`
5. `sidebar-glass` — `.glass-sidebar` alpha + blur
6. `no-broken-images`
7. `button-variants` — ≤6 distinct button styles
8. `h1-min-size` — ≥16px
9. `console-clean` — filtered console errors
10. `main-surface` — primary content glass check

## Running

```powershell
# Full design suite (2k+ scenarios — long run)
cd dotnet-frontend
npm run test:e2e -- e2e/design/

# Sample subset (4 routes × 3 viewports — fast smoke)
$env:DESIGN_SAMPLE = "1"
npm run test:e2e -- e2e/design/route-viewport-matrix.spec.js e2e/design/screenshot-regression.spec.js

# Single spec
npm run test:e2e -- e2e/design/components.spec.js
```

Screenshots land at `D:\Projects\Sarvik\Care-Flow\screenshots\<timestamp>\design\` and sync to `latest\` via `global-teardown.js`.

## Programmatic count

```js
const { computeScenarioCounts } = require("./helpers/design-matrix");
console.log(computeScenarioCounts({ routeCount: 21 }));
// → { total: 2000, ... }
```

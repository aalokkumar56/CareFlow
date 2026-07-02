# Analytics E2E Report — Missed Revenue (`/missed-revenue`)

**Run date:** 2026-06-30  
**Spec:** `dotnet-frontend/e2e/analytics-finance.spec.js`, `mobile-audit.spec.js` (analytics case)  
**Result:** 4/4 passed

---

## Data sanity summary

### API endpoint

`GET /api/dashboard/missed-revenue` (`DashboardService.GetMissedRevenueAsync`)

### Stat card formulas (verified in E2E)

| Stat card | Frontend source | Backend formula | Sensible? |
|-----------|-----------------|-----------------|-----------|
| **Total Estimated Loss** | `data.estimated_loss` | Sum of four category losses | Yes |
| **Accountability Items** | Sum of array lengths | `unanswered + missed_appts + followups + inactive` | Yes |
| **Potentially Recoverable** | `data.recoverable_revenue` | `round(estimated_loss × 0.65)` | Yes (heuristic) |

### Category loss defaults (₹ INR)

| Category | Query criteria | Per-item loss |
|----------|----------------|---------------|
| Unanswered inquiries | `AwaitingReplySince` &gt; 15 min ago | ₹500 flat |
| Missed appointments | Status = `no_show` | `ConsultationFee` if &gt; 0, else ₹1,500 |
| Overdue follow-ups | Pending tasks with `DueAt` &lt; now | ₹800 flat |
| Inactive patients | Status = `re_engagement` (90+ days) | ₹2,000 flat |

### Sample live data (2026-06-30 run)

```
estimated_loss:      ₹22,100  (= 8,500 + 13,600 + 0 + 0)
recoverable_revenue: ₹14,365  (= round(22,100 × 0.65))
accountability_items: 34       (= 0 + 17 + 17 + 0)
```

Category totals match item counts and per-item sums. UI values matched API exactly in E2E.

### Currency formatting

- Frontend uses `formatRupee`: `₹` prefix + `en-IN` locale grouping (e.g. `₹22,100`).
- E2E parses digits only for numeric comparison; display formatting is correct.

### Empty states

| Condition | UI behavior |
|-----------|-------------|
| `totalItems === 0` | Full-page `EmptyState` with illustration + “Back to dashboard” CTA (`data-testid="analytics-empty-state"`) |
| Per-section empty | Compact inline message (e.g. “All patients recently engaged”) — reduced padding on mobile after fix |
| API failure | “Unable to load analytics” glass card |
| Loading | “Loading analytics…” glass card |

### Notes / limitations

- Lists capped at 100 rows per category server-side; totals reflect full query result set within that cap.
- Recoverable revenue is a fixed 65% heuristic, not tied to individual item recoverability.
- Missed-appointment loss uses actual `ConsultationFee` when present (sample data: ₹500 each).

---

## Mobile layout fixes (Task 2)

### Changes

1. **`StatCard.jsx`** — Added `compact` prop: horizontal icon+value layout, `p-2.5` on mobile / `p-6` on `sm+`.
2. **`MissedRevenue.jsx`**
   - Stat grid: `grid-cols-2 sm:grid-cols-3`; third card spans full width on mobile (`col-span-2 sm:col-span-1`).
   - All stat cards use `compact`.
   - Section headers/empty rows tightened for mobile (`py-2.5`, smaller captions).
   - Empty-state uses `compact` prop.
3. **`AppShell.jsx`**
   - Header: `flex-nowrap`, trailing actions `shrink-0` (bell stays on same row as hamburger + title).
   - Tighter mobile padding (`px-3`, `py-2.5`).
   - Subtitle hidden below `sm` when `compactFooter` (Analytics page).

### E2E mobile checks (375×812)

- No horizontal overflow
- Stat card height &lt; 80px (compact)
- Mobile nav toggle works
- Page title “Analytics” visible

---

## Test results

```
analytics-finance › API data formulas are internally consistent          PASS
analytics-finance › missed revenue page shows financial totals matching API  PASS
analytics-finance › missed revenue mobile layout — no overflow, compact stats  PASS
mobile-audit › analytics — stat cards and nav on mobile                    PASS
```

Screenshots saved under `D:\Projects\Sarvik\Care-Flow\screenshots\2026-06-30_03-26\` (copied to `latest\`).

### Screenshot diff vs previous run (`2026-06-29_12-15`)

- **NEW:** 5 design reference screenshots (unrelated to this run)
- **REMOVED:** 30 prior `glassmorphism-audit` screenshots from older folder structure
- No analytics-specific regressions flagged

---

## Files touched

- `dotnet-frontend/src/pages/MissedRevenue.jsx`
- `dotnet-frontend/src/components/glass/StatCard.jsx`
- `dotnet-frontend/src/components/layout/AppShell.jsx`
- `dotnet-frontend/e2e/analytics-finance.spec.js`
- `dotnet-frontend/e2e/mobile-audit.spec.js`

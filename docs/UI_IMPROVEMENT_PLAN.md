# CureFlow UI Improvement Plan

**Audit date:** June 2026  
**Archetype:** Organic & Earthy (design_guidelines.json)  
**Canonical visual reference:** `dotnet-frontend/public/design/design-system-swatches.svg`

---

## Executive summary

The app currently leans heavily into **glassmorphism and purple/indigo accents** that conflict with the **forest-green healthcare brand** in `design_guidelines.json`. Dense operational pages (Patients, Follow-ups kanban, Analytics) suffer from **low contrast borders**, **tiny typography** (9–11px labels), and **weak visual hierarchy**. Settings and notification preferences feel **cramped** with thin dividers and insufficient section breathing room.

This plan prioritizes **usability feeling** — scanability, touch comfort, keyboard access, and calm clarity — over token tweaks alone.

---

## Issues identified

### Visual hierarchy (weak)

| Area | Problem |
|------|---------|
| Dashboard stat cards | Label and value compete; trend text at 10px is easy to miss |
| Page headers | Greeting and subtitle similar weight; breadcrumb path unclear on inner pages |
| Kanban columns | Column headers (10–12px) smaller than card body; counts don't stand out |
| Analytics sections | Section subtitle and row metadata same muted tone |

### Glass / blur noise (too much)

- `mesh-gradient-bg` + `glass-card` + `backdrop-blur(20–24px)` stack on every surface
- `border-white/40` dividers disappear on gradient backgrounds
- Purple box-shadows (`rgba(139, 92, 246, …)`) clash with forest-green brand
- Data tables use `bg-white/75 backdrop-blur` sticky headers — text smears on scroll

### Spacing (inconsistent)

- Settings content: `p-4` with `space-y-3` rows feels tight vs dashboard `gap-4`
- Patients filter bar `gap-1.5` vs table `py-2` row padding
- Kanban `gap-1` in compact mode vs `p-1.5` card padding

### Scanability (kanban / lists)

- Kanban empty columns: dashed border + 11px gray text — no affordance
- Patient table: 8 columns on `min-w-[860px]` — horizontal scroll without sticky first column
- Status pills at 9px are hard to read at a glance
- Selected row uses indigo tint, not brand primary

### Settings pages (cramped)

- Notifications: category headings flush against toggle rows; `border-white/30` dividers invisible
- Role-defaults matrix: `min-w-[480px]` horizontal scroll without sticky type column
- Tab list uses glass styling — low contrast inactive tabs

### Secondary text contrast (low)

- `text-text-muted` (#9CA3AF) on glass/gradient fails WCAG AA for small sizes
- Chart axis labels at 10px in `#94A3B8`
- `text-ui-caption` (10px) used for meaningful metadata (due dates, loss amounts)

### Touch targets (mobile)

- Sidebar nav: `py-2.5` ≈ 40px total height — below 44px guideline
- Kanban card actions: `p-1` buttons (~28px)
- Table action menu: correct at 36px (`h-9 w-9`) but filter selects are `h-9` (36px)
- Mobile nav toggle: `p-2` on icon only

---

## Prioritized recommendations

### Quick wins (1–2 days) — **implemented in this pass**

1. **Sidebar**
   - Replace gradient sparkle logo with `cureflow-logo.svg`
   - Active state: brand green left border + `bg-primary-soft`, not purple gradient
   - Nav links `min-h-[44px]` for touch
   - Visible `focus-visible:ring-2 ring-[#064E3B]/30`

2. **Empty states**
   - Wire SVG illustrations into Patients, Follow-ups, Analytics when lists are empty
   - Add title + helper text + primary CTA where applicable

3. **Cards on data pages**
   - New `GlassCard variant="solid"`: white surface, `border-[#E5E7EB]`, no blur
   - Apply to Patients table container, Follow-ups kanban wrapper

4. **Typography baseline**
   - Body minimum 13px (`text-ui-base`), paragraph `line-height: 1.5`
   - Bump muted secondary from #9CA3AF usage on small text → `text-text-secondary` (#4B5563)

5. **Focus states**
   - Global `focus-visible` ring on buttons, links, inputs (`ring-[#064E3B]/25`)

6. **Dashboard stat cards**
   - Larger metric (`text-2xl`), uppercase label at 11px with `text-text-secondary`
   - Muted trend line, emerald only on delta

7. **Settings → Notifications**
   - Section cards with `border-[#E5E7EB]` dividers
   - Increased row padding (`py-3`), category headers with bottom border

### Medium effort (3–5 days)

1. **Theme reconciliation**
   - Replace `sidebar-nav-active` purple gradient in CSS with brand tokens
   - Tone down `mesh-gradient-bg` on non-dashboard routes (solid `#FDFDFC` for Patients/Settings)
   - Remove violet box-shadows from `.glass-card`; use 1px border only per guidelines

2. **Kanban redesign**
   - Column headers: fixed height 48px, count badge pill
   - Card borders `border-[#E5E7EB]`, solid white background
   - Sticky column headers on horizontal scroll (mobile)
   - Drag handle affordance (grip icon)

3. **Patient table**
   - Sticky first column (name + avatar)
   - Row zebra optional: `even:bg-[#FAFAFA]`
   - Collapse "Total Spent" on `< lg` breakpoints
   - Preview panel as bottom sheet on mobile

4. **Settings layout**
   - Increase `SettingsLayout` content max-width readability
   - Consistent `gap-6` between sections
   - Settings nav: active state matches main sidebar

5. **Command palette & search**
   - Solid input on data pages (less glass-input blur)
   - Higher contrast placeholder text

### Large effort (1–2 sprints)

1. **Design system alignment**
   - Migrate from glass-dashboard mockup to guidelines "Cardless or Flat Solid"
   - CSS variables for all semantic colors; deprecate indigo accents except charts
   - Component library doc from `design-system-swatches.svg`

2. **Responsive navigation**
   - Bottom tab bar on mobile for top 4 destinations
   - Collapsible sidebar with icons-only mode on tablet

3. **Dashboard information architecture**
   - Single-column mobile story: stats → appointments → revenue
   - Optional `dashboard-hero-pattern.svg` at 3% opacity behind header only

4. **Accessibility audit**
   - Full keyboard path through kanban (not drag-only)
   - Screen reader labels for stat trends
   - Color-blind safe status palette

5. **Login & marketing assets**
   - Replace raster login background with SVG pattern export
   - Local `cureflow-logo.svg` everywhere (favicon, PWA manifest)

---

## SVG design assets

All assets live in `dotnet-frontend/public/design/`:

| File | Purpose |
|------|---------|
| `cureflow-logo.svg` | Wordmark + healthcare icon for sidebar, docs |
| `empty-state-patients.svg` | Empty patient list |
| `empty-state-tasks.svg` | Empty follow-ups kanban |
| `empty-state-analytics.svg` | Analytics all-clear state |
| `dashboard-hero-pattern.svg` | Subtle background pattern (optional) |
| `design-system-swatches.svg` | Stakeholder-facing spec: colors, type, buttons, radius |

**Figma-style mockups:** Full page mockups can be exported as SVG from Figma or built in code; `design-system-swatches.svg` is the **canonical visual reference** for colors, typography scale, button styles, and border radii until a dedicated design file exists.

---

## Success metrics (usability)

- Sidebar nav items pass 44×44px touch target on mobile sheet
- Body text ≥ 13px on all operational pages
- Empty states provide clear next action (not gray sentence only)
- Focus ring visible on Tab through main nav and form controls
- Stakeholders can review brand without opening the app (swatches SVG)

---

## Files touched (implementation pass)

- `dotnet-frontend/src/index.css` — focus, solid card, sidebar active, typography
- `dotnet-frontend/src/components/layout/Sidebar.jsx`
- `dotnet-frontend/src/components/glass/GlassCard.jsx`
- `dotnet-frontend/src/components/glass/DashboardStatCard.jsx`
- `dotnet-frontend/src/components/glass/KanbanBoard.jsx`
- `dotnet-frontend/src/components/ui/EmptyState.jsx` (new)
- `dotnet-frontend/src/pages/Patients.jsx`
- `dotnet-frontend/src/pages/Tasks.jsx`
- `dotnet-frontend/src/pages/MissedRevenue.jsx`
- `dotnet-frontend/src/components/notifications/NotificationPreferencesContent.jsx`

const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const {
  VIEWPORTS,
  getDesignRoutes,
  routeSlug,
  viewportSlug,
  gotoDesignRoute,
  GLASS_SURFACE_CHECKS,
  KANBAN_BOARDS,
} = require("../helpers/design-matrix");
const {
  isGlassLikeSurface,
  sampleSurfaceStyles,
  auditKanbanColumnGlass,
  auditKanbanCardGlass,
} = require("../helpers/ui-audit");
const { reportBug } = require("../helpers/bug-log");

test.describe.configure({ mode: "serial" });

test.describe("Glass vs solid surface audit", () => {
  for (const viewport of VIEWPORTS) {
    for (const routePath of getDesignRoutes({})) {
      if (routePath === "/login") continue;

      for (const { selector, label, expectGlass } of GLASS_SURFACE_CHECKS) {
        test(`${routeSlug(routePath)}@${viewportSlug(viewport)} — ${label}`, async ({ page }) => {
          await login(page);
          await gotoDesignRoute(page, routePath, viewport);
          const styles = await sampleSurfaceStyles(page, selector);
          if (!styles) return;
          const isGlass = isGlassLikeSurface(styles);
          if (expectGlass && !isGlass) {
            reportBug({
              severity: label === "sidebar" ? "High" : "Medium",
              category: "Glassmorphism",
              page: routePath,
              title: `${label} not glass at ${viewport.id}`,
              actual: styles.backgroundColor,
            });
          }
          if (expectGlass) expect(isGlass).toBe(true);
        });
      }
    }
  }

  for (const viewport of VIEWPORTS) {
    for (const { path, boardTestId, label } of KANBAN_BOARDS) {
      test(`kanban columns ${label} @ ${viewportSlug(viewport)}`, async ({ page }) => {
        await login(page);
        await gotoDesignRoute(page, path, viewport);
        await expect(page.getByTestId(boardTestId)).toBeVisible({ timeout: 20_000 });
        const audit = await auditKanbanColumnGlass(page, boardTestId);
        for (const issue of audit.issues) {
          reportBug({
            severity: "High",
            category: "Glassmorphism",
            page: path,
            title: `${label} column ${issue.columnIndex + 1} solid @ ${viewport.id}`,
            actual: issue.backgroundColor,
          });
        }
      });

      test(`kanban cards ${label} @ ${viewportSlug(viewport)}`, async ({ page }) => {
        await login(page);
        await gotoDesignRoute(page, path, viewport);
        await expect(page.getByTestId(boardTestId)).toBeVisible({ timeout: 20_000 });
        const audit = await auditKanbanCardGlass(page, boardTestId);
        if (!audit.hasCard) return;
        for (const issue of audit.issues) {
          reportBug({
            severity: "Medium",
            category: "Glassmorphism",
            page: path,
            title: `${label} card solid @ ${viewport.id}`,
            actual: issue.backgroundColor,
          });
        }
      });
    }
  }
});

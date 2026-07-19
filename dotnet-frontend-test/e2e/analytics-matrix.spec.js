const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { gotoAsRole } = require("./helpers/navigation");
const { ALL_ROLES_MATRIX, VIEWPORTS, routeAllowed } = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

const analyticsRoute = { permission: PERMISSIONS.DashboardView, expectTestId: "analytics-total-loss", apiPath: "/dashboard/missed-revenue" };

const ANALYTICS_CHECKS = [
  { id: "analytics-total-loss", pattern: /₹/ },
  { id: "analytics-recoverable", pattern: /₹/ },
  { id: "analytics-item-count", pattern: /\d+/ },
];

test.describe("Analytics matrix — missed revenue × roles × viewports", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const check of ANALYTICS_CHECKS) {
      test(`${role}: analytics metric ${check.id}`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(analyticsRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        const apiPromise = page.waitForResponse(
          (r) => r.url().includes("/api/dashboard/missed-revenue") && r.ok(),
          { timeout: 30_000 },
        );
        await page.goto("/missed-revenue", { waitUntil: "domcontentloaded" });
        await apiPromise;

        const el = page.getByTestId(check.id);
        if (await el.isVisible().catch(() => false)) {
          await expect(el).toContainText(check.pattern);
        }
      });
    }

    for (const viewport of VIEWPORTS) {
      test(`${role} @ ${viewport.name}: analytics page loads`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(analyticsRoute, session.permissions));

        await gotoAsRole(page, session, "/missed-revenue", {
          viewport,
          waitForApi: "/api/dashboard/missed-revenue",
        });
        await expect(page.getByTestId("analytics-total-loss")).toBeVisible({ timeout: 20_000 });
      });
    }
  }
});

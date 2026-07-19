const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const {
  ALL_ROLES_MATRIX,
  DASHBOARD_APPT_PERIODS,
  DASHBOARD_REVENUE_PERIODS,
  DASHBOARD_WIDGETS,
  routeAllowed,
} = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

const dashRoute = { permission: PERMISSIONS.DashboardView };

test.describe("Dashboard matrix — period filters × widgets × roles", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const periodId of DASHBOARD_APPT_PERIODS) {
      test(`${role}: appointments chart period ${periodId}`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(dashRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        const overviewPromise = page.waitForResponse(
          (r) => r.url().includes("/api/dashboard/overview") && r.ok(),
          { timeout: 30_000 },
        );
        await page.goto("/", { waitUntil: "domcontentloaded" });
        await overviewPromise;

        await page.getByTestId("dashboard-appt-week-toggle").click();
        await page.getByTestId(`dashboard-appt-period-${periodId}`).click();
        await expect(page.getByTestId("dashboard-appt-chart")).toBeVisible();
      });
    }

    for (const periodId of DASHBOARD_REVENUE_PERIODS) {
      test(`${role}: revenue period ${periodId}`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(dashRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/", { waitUntil: "networkidle" });

        await page.getByTestId("dashboard-revenue-period-toggle").click();
        await page.getByTestId(`dashboard-revenue-period-${periodId}`).click();
        await expect(page.getByTestId("dashboard-revenue-mtd-value")).toContainText(/₹/);
      });
    }

    for (const widgetId of DASHBOARD_WIDGETS) {
      test(`${role}: widget ${widgetId} visible`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(dashRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/", { waitUntil: "networkidle" });
        await expect(page.getByTestId(widgetId)).toBeVisible({ timeout: 20_000 });
      });
    }
  }
});

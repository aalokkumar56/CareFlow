const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { gotoAsRole } = require("./helpers/navigation");
const {
  ALL_ROLES_MATRIX,
  SEARCH_QUERIES,
  KANBAN_PAGES,
  VIEWPORTS,
  routeAllowed,
} = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../src/lib/permissions");

test.describe.configure({ mode: "serial" });

const apptRoute = { permission: PERMISSIONS.AppointmentView };
const apptKanban = KANBAN_PAGES[0];

test.describe("Appointments matrix — search × kanban × viewports × roles", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const query of SEARCH_QUERIES) {
      test(`${role}: appointment search "${query || "(empty)"}"`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(apptRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/appointments", { waitUntil: "networkidle" });

        const searchInput = page.getByTestId("appt-search");
        if (!(await searchInput.isVisible().catch(() => false))) return;

        const searchPromise = page.waitForResponse(
          (r) => r.url().includes("/api/appointments") && r.ok(),
          { timeout: 15_000 },
        );
        await searchInput.fill(query);
        await searchPromise;
      });
    }

    for (const column of apptKanban.columns) {
      test(`${role}: kanban column "${column}" visible`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(apptRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/appointments", { waitUntil: "networkidle" });
        await expect(page.getByTestId(apptKanban.testId)).toBeVisible();
        await expect(page.getByText(column, { exact: false }).first()).toBeVisible({ timeout: 10_000 });
      });
    }

    for (const viewport of VIEWPORTS) {
      test(`${role} @ ${viewport.name}: appointments kanban renders`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(apptRoute, session.permissions));

        await gotoAsRole(page, session, "/appointments", { viewport });
        await expect(page.getByTestId(apptKanban.testId)).toBeVisible({ timeout: 20_000 });
      });
    }
  }
});

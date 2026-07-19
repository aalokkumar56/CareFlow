const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { gotoAsRole } = require("./helpers/navigation");
const {
  ALL_ROLES_MATRIX,
  SEARCH_QUERIES,
  VIEWPORTS,
  routeAllowed,
} = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

const staffRoute = {
  anyPermission: [PERMISSIONS.StaffView, PERMISSIONS.ClinicalView],
};

test.describe("Staff matrix — search × viewports × roles", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const query of SEARCH_QUERIES) {
      test(`${role}: staff search "${query || "(empty)"}"`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(staffRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/staff", { waitUntil: "networkidle" });

        const searchPromise = page.waitForResponse(
          (r) => r.url().includes("/api/staff") && r.ok(),
          { timeout: 15_000 },
        );
        await page.getByTestId("staff-search").fill(query);
        await searchPromise;
      });
    }

    for (const viewport of VIEWPORTS) {
      test(`${role} @ ${viewport.name}: staff list loads`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(staffRoute, session.permissions));

        await gotoAsRole(page, session, "/staff", { viewport });
        await expect(page.getByTestId("staff-search")).toBeVisible({ timeout: 20_000 });
      });
    }
  }
});

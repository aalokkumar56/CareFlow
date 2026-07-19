const { test, expect } = require("@playwright/test");
const { getRoleSessions } = require("./helpers/role-session");
const { gotoAsRole } = require("./helpers/navigation");
const {
  ALL_ROLES_MATRIX,
  SETTINGS_SUB_ROUTES,
  VIEWPORTS,
  routeAllowed,
} = require("./helpers/test-matrix");

test.describe.configure({ mode: "serial" });

test.describe("Settings matrix — routes × roles × viewports", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const route of SETTINGS_SUB_ROUTES) {
      for (const viewport of VIEWPORTS) {
        test(`${role} @ ${viewport.name}: ${route.label}`, async ({ page }) => {
          const session = roles[role];
          const allowed = routeAllowed(route, session.permissions);

          const { pathname } = await gotoAsRole(page, session, route.path, {
            viewport,
            waitForApi: allowed && route.path.includes("/settings") ? undefined : undefined,
          });

          if (!allowed) {
            expect(pathname).not.toBe(route.path);
            return;
          }

          expect(pathname).toBe(route.path);
          if (route.expectTestId) {
            await expect(page.getByTestId(route.expectTestId)).toBeVisible({ timeout: 20_000 });
          }
        });
      }
    }
  }
});

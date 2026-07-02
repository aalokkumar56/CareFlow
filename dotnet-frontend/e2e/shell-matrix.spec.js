const { test, expect } = require("@playwright/test");
const { getRoleSessions } = require("./helpers/role-session");
const { gotoAsRole } = require("./helpers/navigation");
const { ALL_ROLES_MATRIX, VIEWPORTS, routeAllowed } = require("./helpers/test-matrix");
const { APPSHELL_PAGES } = require("./helpers/routes");
const { MAIN_ROUTES, SETTINGS_ROUTES } = require("./helpers/routes");

test.describe.configure({ mode: "serial" });

function routeMeta(path) {
  return MAIN_ROUTES.find((r) => r.path === path)
    || SETTINGS_ROUTES.find((r) => r.path === path)
    || { path, permission: null };
}

test.describe("Shell matrix — AppShell chrome × roles × viewports", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const shellPage of APPSHELL_PAGES) {
      for (const viewport of VIEWPORTS) {
        test(`${role} @ ${viewport.name}: shell on ${shellPage.label}`, async ({ page }) => {
          const session = roles[role];
          const meta = routeMeta(shellPage.path);
          test.skip(meta.permission && !routeAllowed(meta, session.permissions));

          await gotoAsRole(page, session, shellPage.path, { viewport });

          if (viewport.name === "mobile") {
            await expect(page.getByTestId("mobile-nav-toggle").first()).toBeVisible({ timeout: 20_000 });
          } else {
            await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 20_000 });
          }

          const commandPalette = page.getByTestId("open-command-palette");
          if (shellPage.hideHeaderSearch) {
            await expect(commandPalette).toHaveCount(0);
          } else if (shellPage.path !== "/") {
            await expect(commandPalette.first()).toBeVisible();
          }
        });
      }
    }
  }
});

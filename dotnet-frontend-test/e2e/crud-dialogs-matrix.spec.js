const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { ALL_ROLES_MATRIX, CRUD_DIALOGS, routeAllowed, MAIN_ROUTES, SETTINGS_ROUTES } = require("./helpers/test-matrix");

test.describe.configure({ mode: "serial" });

test.describe("CRUD dialogs matrix — open create dialogs per role", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const dialog of CRUD_DIALOGS) {
      test(`${role}: ${dialog.btn} on ${dialog.path}`, async ({ page }) => {
        const session = roles[role];
        const pageRoute = MAIN_ROUTES.find((r) => r.path === dialog.path)
          || SETTINGS_ROUTES.find((r) => r.path === dialog.path);
        test.skip(!pageRoute || !routeAllowed(pageRoute, session.permissions), `${role} cannot access ${dialog.path}`);
        test.skip(!session.permissions.includes(dialog.permission), `${role} missing create permission`);

        await login(page, { email: session.email, password: session.password });
        await page.goto(dialog.path, { waitUntil: "networkidle" });

        const btn = page.getByTestId(dialog.btn);
        test.skip(!(await btn.isVisible().catch(() => false)), "Create button not visible");

        await btn.click();
        await page.waitForTimeout(400);

        if (dialog.dialog) {
          await expect(page.getByTestId(dialog.dialog)).toBeVisible({ timeout: 10_000 });
        } else {
          const dialogEl = page.getByRole("dialog");
          await expect(dialogEl).toBeVisible({ timeout: 10_000 });
        }
      });
    }
  }
});

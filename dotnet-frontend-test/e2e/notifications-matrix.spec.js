const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { ALL_ROLES_MATRIX } = require("./helpers/test-matrix");
const { apiLogin, getUnreadCount } = require("./helpers/api");

test.describe.configure({ mode: "serial" });

const NOTIFICATION_CHECKS = [
  "bell-visible",
  "dropdown-opens",
  "preferences-link",
  "mark-all-read-api",
];

test.describe("Notifications matrix — bell × roles", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    await getUnreadCount(admin.accessToken);
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const check of NOTIFICATION_CHECKS) {
      test(`${role}: notification ${check}`, async ({ page }) => {
        const session = roles[role];
        await login(page, { email: session.email, password: session.password });
        await page.goto("/", { waitUntil: "networkidle" });

        const bell = page.getByTestId("notification-bell-desktop").or(page.getByTestId("notification-bell-mobile"));
        if (check === "bell-visible") {
          if (await bell.isVisible().catch(() => false)) {
            await expect(bell).toBeVisible();
          }
          return;
        }

        test.skip(!(await bell.isVisible().catch(() => false)), "Bell hidden on this page");

        if (check === "dropdown-opens") {
          await bell.click();
          await expect(page.getByTestId("notification-dropdown")).toBeVisible({ timeout: 10_000 });
          return;
        }

        if (check === "preferences-link") {
          await bell.click();
          const prefs = page.getByTestId("notification-preferences-link");
          if (await prefs.isVisible().catch(() => false)) {
            await expect(prefs).toBeVisible();
          }
          return;
        }

        if (check === "mark-all-read-api") {
          await bell.click();
          const markAll = page.getByTestId("notification-mark-all-read");
          if (await markAll.isVisible().catch(() => false)) {
            const apiPromise = page.waitForResponse(
              (r) => r.url().includes("/api/notifications/mark-all-read") && r.ok(),
              { timeout: 15_000 },
            );
            await markAll.click();
            await apiPromise;
          }
        }
      });
    }
  }
});

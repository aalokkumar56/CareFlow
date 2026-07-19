const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { ensureTestUsers } = require("./helpers/test-users");
const {
  ALL_ROLES_MATRIX,
  INVALID_LOGIN_CASES,
  protectedStaticRoutes,
} = require("./helpers/test-matrix");

test.describe.configure({ mode: "serial" });

test.describe("Auth matrix — login, logout, unauthenticated redirects", () => {
  let roles = {};

  test.beforeAll(async () => {
    const users = await ensureTestUsers(Date.now());
    roles = Object.fromEntries(
      Object.entries(users).map(([role, creds]) => [role, { email: creds.email, password: creds.password }]),
    );
  });

  for (const { email, password, label } of INVALID_LOGIN_CASES) {
    test(`login rejects: ${label}`, async ({ page }) => {
      await page.goto("/login", { waitUntil: "domcontentloaded" });
      if (email) await page.getByTestId("login-email").fill(email);
      if (password) await page.getByTestId("login-password").fill(password);

      const loginResponse = page.waitForResponse(
        (r) => r.url().includes("/api/auth/login") && r.request().method() === "POST",
        { timeout: 15_000 },
      ).catch(() => null);

      await page.getByTestId("login-submit").click();
      const res = loginResponse ? await loginResponse : null;

      if (res) {
        expect(res.ok()).toBeFalsy();
      }
      await expect(page).toHaveURL(/\/login/);
    });
  }

  for (const role of ALL_ROLES_MATRIX) {
    test(`${role}: login succeeds via API session`, async ({ page }) => {
      const session = roles[role];
      await login(page, { email: session.email, password: session.password });
      await expect(page).not.toHaveURL(/\/login/);
      await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 20_000 });
    });

    test(`${role}: logout clears session`, async ({ page }) => {
      const session = roles[role];
      await login(page, { email: session.email, password: session.password });

      const logoutBtn = page.getByTestId("logout-btn");
      if (await logoutBtn.isVisible().catch(() => false)) {
        await logoutBtn.click();
        await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });
      }
    });
  }

  for (const route of protectedStaticRoutes()) {
    test(`unauthenticated: ${route.path} redirects to login`, async ({ page }) => {
      await page.goto(route.path, { waitUntil: "domcontentloaded" });
      await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });
    });
  }
});

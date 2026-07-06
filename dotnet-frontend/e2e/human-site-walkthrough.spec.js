/**
 * One browser, all roles — login → walk modules → logout → next role.
 * Run:  npm run test:e2e:walkthrough
 *
 * Uses real mouse clicks (human-interaction) — no API token injection, no page.goto shortcuts
 * for in-app navigation.
 */
const { test } = require("@playwright/test");
const { humanLogin, humanLogout } = require("./helpers/human-interaction");
const { walkRoleModules } = require("./helpers/human-walkthrough");
const { ensureTestUsers } = require("./helpers/test-users");
const { ALL_ROLES_MATRIX } = require("./helpers/routes");
const { apiLogin } = require("./helpers/api");
const { getPermissionsForUser } = require("../src/lib/permissions");
const { setupConsoleCapture } = require("./helpers/ui-audit");

test.use({
  launchOptions: { slowMo: 100 },
  video: "on",
});

test.describe.configure({ mode: "serial" });

test.describe("Human multi-role walkthrough (single browser, mouse clicks)", () => {
  /** @type {Record<string, { email: string, password: string, permissions: string[] }>} */
  let roleSessions = {};

  test.beforeAll(async () => {
    const stamp = Date.now();
    const users = await ensureTestUsers(stamp);

    for (const role of ALL_ROLES_MATRIX) {
      const creds = users[role];
      const { user } = await apiLogin(creds.email, creds.password);
      roleSessions[role] = {
        email: creds.email,
        password: creds.password,
        permissions: getPermissionsForUser(user),
      };
    }
  });

  test("all roles walk every permitted module in one browser session", async ({ page }) => {
    test.setTimeout(900_000); // 15 min — 6 roles × full site walk
    const consoleCapture = setupConsoleCapture(page);
    const failures = [];

    // Warm up Next.js compilation before first real login
    await page.goto("/login", { waitUntil: "domcontentloaded" });
    await page.getByTestId("login-form").waitFor({ state: "visible", timeout: 60_000 });

    for (const role of ALL_ROLES_MATRIX) {
      const session = roleSessions[role];
      console.log(`\n[walkthrough] ── Role: ${role} (${session.email}) ──`);

      try {
        await humanLogin(page, session.email, session.password);
        await walkRoleModules(page, session.permissions, { roleLabel: role });
        await humanLogout(page);
        console.log(`[walkthrough] ${role}: logout OK`);
      } catch (err) {
        failures.push({ role, error: err.message });
        console.error(`[walkthrough] ${role} FAILED:`, err.message);
        // Try to recover to login page for next role
        await page.goto("/login", { waitUntil: "domcontentloaded" }).catch(() => {});
      }
    }

    const errors = consoleCapture.getFilteredErrors();
    if (errors.length > 0) {
      console.warn("[walkthrough] Console errors (non-fatal):\n", errors.join("\n"));
    }

    if (failures.length > 0) {
      throw new Error(
        `Walkthrough failures:\n${failures.map((f) => `  ${f.role}: ${f.error}`).join("\n")}`,
      );
    }
  });
});

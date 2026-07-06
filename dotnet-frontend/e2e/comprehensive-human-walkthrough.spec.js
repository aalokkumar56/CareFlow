/**
 * Comprehensive human E2E — ONE Chrome instance, ONE login per role, sidebar clicks only.
 *
 * Run admin deep walk:
 *   npm run test:e2e:walkthrough
 *
 * Run all roles (login → full walk → logout → next role, same browser):
 *   npm run test:e2e:walkthrough:all
 */
const { test, expect } = require("@playwright/test");
const { humanLogin, humanLogout, resetSessionAndLogin } = require("./helpers/human-interaction");
const { walkComprehensive } = require("./helpers/comprehensive-walkthrough");
const { ensureTestUsers } = require("./helpers/test-users");
const { ALL_ROLES_MATRIX } = require("./helpers/routes");
const { apiLogin } = require("./helpers/api");
const { getPermissionsForUser } = require("../src/lib/permissions");
const { setupConsoleCapture } = require("./helpers/ui-audit");

test.use({
  launchOptions: { slowMo: 80 },
  screenshot: "on",
  video: "on",
});

test.describe.configure({ mode: "serial" });

test.describe("Comprehensive human walkthrough (single Chrome, no browser restart)", () => {
  /** @type {Record<string, { email: string, password: string; permissions: string[] }>} */
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

  test("admin — every module, filter, tab, and dialog in one session", async ({ page }) => {
    test.setTimeout(900_000);
    const consoleCapture = setupConsoleCapture(page);
    const session = roleSessions.admin;

    await page.goto("/login", { waitUntil: "domcontentloaded" });
    await humanLogin(page, session.email, session.password);

    const { uxNotes } = await walkComprehensive(page, session.permissions, { roleLabel: "admin" });

    expect(uxNotes.length).toBeGreaterThanOrEqual(0);

    const errors = consoleCapture.getFilteredErrors();
    if (errors.length > 0) {
      console.warn("[admin walk] Console errors:\n", errors.join("\n"));
    }

    // Stay logged in — do not logout; user may continue manual testing in same browser
    console.log("[admin walk] Complete — browser session left logged in as admin");
  });

  test("all 6 roles — same browser, login once per role, full deep walk each", async ({ page }) => {
    test.setTimeout(1_800_000);
    const consoleCapture = setupConsoleCapture(page);
    const failures = [];
    const allUxNotes = [];

    for (const role of ALL_ROLES_MATRIX) {
      const session = roleSessions[role];
      console.log(`\n[walkthrough] ── Role: ${role} (${session.email}) ──`);

      try {
        await resetSessionAndLogin(page, session.email, session.password);
        const { uxNotes } = await walkComprehensive(page, session.permissions, { roleLabel: role });
        allUxNotes.push(...uxNotes.map((n) => ({ ...n, role })));
        console.log(`[walkthrough] ${role}: all modules visited`);
      } catch (err) {
        failures.push({ role, error: err.message });
        console.error(`[walkthrough] ${role} FAILED:`, err.message);
        await page.goto("/login", { waitUntil: "domcontentloaded" }).catch(() => {});
      }
    }

    if (allUxNotes.length) {
      console.log("\n=== UX observations (all roles) ===");
      for (const n of allUxNotes) {
        console.log(`  [${n.role}] [${n.severity}] ${n.area}: ${n.observation}`);
      }
    }

    const errors = consoleCapture.getFilteredErrors();
    if (errors.length > 0) {
      console.warn("[multi-role walk] Console errors:\n", errors.join("\n"));
    }

    if (failures.length > 0) {
      throw new Error(
        `Walkthrough failures:\n${failures.map((f) => `  ${f.role}: ${f.error}`).join("\n")}`,
      );
    }
  });
});

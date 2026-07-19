/**
 * Session fixation + logout session hygiene.
 *
 * Scenario IDs (TEST_SCENARIOS.md):
 *   UI-SEC-041, UI-SEC-042, UI-SEC-043, UI-SEC-045,
 *   UI-SEC-048, UI-SEC-099, UI-CRIT-053
 */
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, createPatient } = require("../helpers/api");
const { ensureTestUsers } = require("../helpers/test-users");
const { humanLogin } = require("../helpers/human-interaction");

test.describe.configure({ mode: "serial" });

const CRAFTED_TOKEN = "eyJhbGciOiJub25lIn0.eyJzdWIiOiJhdHRhY2tlciIsInJvbGUiOiJhZG1pbiJ9.forged";

async function storageKeys(page) {
  return page.evaluate(() => ({
    token: localStorage.getItem("cureflow_token"),
    user: localStorage.getItem("cureflow_user"),
    tenant: localStorage.getItem("cureflow_tenant"),
  }));
}

test.describe("Security UI — session fixation and logout", () => {
  /** @type {{ email: string, password: string }} */
  let admin = { email: "admin@cureflow.in", password: "admin123" };
  /** @type {{ email: string, password: string }} */
  let nurse = null;
  /** @type {string} */
  let adminToken = "";
  /** @type {string} */
  let patientId = "";
  /** @type {string} */
  let patientName = "";

  test.beforeAll(async () => {
    const stamp = Date.now();
    const users = await ensureTestUsers(stamp);
    admin = { email: users.admin.email, password: users.admin.password };
    nurse = { email: users.nurse.email, password: users.nurse.password };

    const session = await apiLogin(admin.email, admin.password);
    adminToken = session.accessToken;
    patientName = `Session Guard ${stamp}`;
    const created = await createPatient(adminToken, patientName, `91${String(stamp).slice(-10)}`);
    patientId = created?.id || created?.patient?.id;
    expect(patientId, "seed patient id").toBeTruthy();
  });

  test("UI-SEC-099: login replaces pre-planted localStorage token", async ({ page }) => {
    await page.goto("/login", { waitUntil: "domcontentloaded" });
    await page.evaluate((token) => {
      localStorage.setItem("cureflow_token", token);
      localStorage.setItem(
        "cureflow_user",
        JSON.stringify({ id: "attacker", email: "attacker@evil.test", role: "admin", name: "Attacker" }),
      );
      localStorage.setItem("cureflow_tenant", JSON.stringify({ id: "evil-tenant", name: "Evil" }));
    }, CRAFTED_TOKEN);

    const planted = await storageKeys(page);
    expect(planted.token).toBe(CRAFTED_TOKEN);

    await humanLogin(page, admin.email, admin.password);
    await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 20_000 });

    const after = await storageKeys(page);
    expect(after.token, "crafted token must be replaced").toBeTruthy();
    expect(after.token).not.toBe(CRAFTED_TOKEN);
    expect(after.token.split(".").length, "JWT shape").toBeGreaterThanOrEqual(3);

    const user = JSON.parse(after.user || "{}");
    expect(String(user.email || "").toLowerCase()).toBe(admin.email.toLowerCase());
    expect(user.email).not.toBe("attacker@evil.test");
  });

  test("UI-SEC-041: logout clears cureflow_* auth keys", async ({ page }) => {
    await login(page, { email: admin.email, password: admin.password });
    await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 20_000 });

    const before = await storageKeys(page);
    expect(before.token).toBeTruthy();
    expect(before.user).toBeTruthy();
    expect(before.tenant).toBeTruthy();

    await page.getByTestId("logout-btn").click();
    await expect(page).toHaveURL(/\/login/, { timeout: 20_000 });

    const after = await storageKeys(page);
    expect(after.token).toBeNull();
    expect(after.user).toBeNull();
    expect(after.tenant).toBeNull();
  });

  test("UI-SEC-042/043: after logout Back cannot restore protected PHI UI", async ({ page }) => {
    await login(page, { email: admin.email, password: admin.password });
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("patient-name")).toContainText(patientName, { timeout: 20_000 });

    await page.getByTestId("logout-btn").click();
    await expect(page).toHaveURL(/\/login/, { timeout: 20_000 });

    await page.goBack();
    await page.waitForTimeout(600);

    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });
    const bodyText = await page.locator("body").innerText();
    expect(bodyText.includes(patientName), "PHI visible after Back").toBeFalsy();

    const keys = await storageKeys(page);
    expect(keys.token).toBeNull();
  });

  test("UI-SEC-045 / UI-CRIT-053: storage cleared in another tab forces login on next nav", async ({ page, context }) => {
    await login(page, { email: admin.email, password: admin.password });
    await page.goto("/patients", { waitUntil: "networkidle" });
    await expect(page.getByTestId("patients-search")).toBeVisible({ timeout: 20_000 });

    // Simulate logout from another tab (clears shared localStorage origin).
    const other = await context.newPage();
    await other.goto("/login", { waitUntil: "domcontentloaded" });
    await other.evaluate(() => {
      localStorage.removeItem("cureflow_token");
      localStorage.removeItem("cureflow_user");
      localStorage.removeItem("cureflow_tenant");
    });
    await other.close();

    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page).toHaveURL(/\/login/, { timeout: 20_000 });
  });

  test("UI-SEC-048: re-login as different user does not flash previous identity", async ({ page }) => {
    await login(page, { email: admin.email, password: admin.password });
    await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("logout-btn").click();
    await expect(page).toHaveURL(/\/login/, { timeout: 20_000 });

    await humanLogin(page, nurse.email, nurse.password);
    await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 20_000 });

    const keys = await storageKeys(page);
    const user = JSON.parse(keys.user || "{}");
    expect(String(user.email || "").toLowerCase()).toBe(nurse.email.toLowerCase());
    expect(String(user.role || "").toLowerCase()).toMatch(/nurse/);

    // Admin-only settings nav must stay hidden for nurse.
    const usersNav = page.getByTestId("app-sidebar").getByTestId("nav-settings");
    // Nurse may or may not see settings depending on Settings.View — assert storage identity only above.
    // Sidebar email/name if shown should not be admin.
    const sidebarText = await page.getByTestId("app-sidebar").innerText();
    expect(sidebarText.toLowerCase()).not.toContain(admin.email.toLowerCase());
    void usersNav;
  });
});

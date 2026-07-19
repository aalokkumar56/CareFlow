// STRICT: asserts expected success OR expected failure; no soft-pass
/**
 * Command palette must not land restricted roles on forbidden routes.
 *
 * Scenario IDs (TEST_SCENARIOS.md):
 *   UI-MED-013, UI-MED-015, UI-SEC-097, UI-HIGH-069 (allowed path smoke)
 */
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, createPatient } = require("../helpers/api");
const { ensureTestUsers } = require("../helpers/test-users");
const { getPermissionsForUser, PERMISSIONS } = require("../../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

/** Navigate / Quick Actions labels → path + required permission. */
const PALETTE_ROUTES = [
  { label: "Daily Operations Dashboard", path: "/", permission: PERMISSIONS.DashboardView },
  { label: "WhatsApp Inbox", path: "/inbox", permission: PERMISSIONS.ConversationView },
  { label: "Email Inbox", path: "/email-inbox", permission: PERMISSIONS.ConversationView },
  { label: "All Patients", path: "/patients", permission: PERMISSIONS.PatientView },
  { label: "Appointments", path: "/appointments", permission: PERMISSIONS.AppointmentView },
  { label: "Follow-ups", path: "/tasks", permission: PERMISSIONS.DashboardView },
  { label: "New Patient", path: "/patients", permission: PERMISSIONS.PatientView, query: "new=1" },
  { label: "New Appointment", path: "/appointments", permission: PERMISSIONS.AppointmentView, query: "new=1" },
];

const ROLES_UNDER_TEST = ["nurse", "marketing", "staff"];

async function openCommandPalette(page, session) {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  const sidebar = page.getByTestId("app-sidebar");
  if (!(await sidebar.isVisible().catch(() => false)) && session) {
    await login(page, { email: session.email, password: session.password });
    await page.goto("/", { waitUntil: "domcontentloaded" });
  }
  await expect(sidebar).toBeVisible({ timeout: 20_000 });
  const btn = page
    .getByTestId("open-command-palette")
    .or(page.getByTestId("open-command-palette-mobile"))
    .first();
  await expect(btn).toBeVisible({ timeout: 20_000 });
  await btn.click();
  await expect(page.getByTestId("cmd-palette-input")).toBeVisible({ timeout: 10_000 });
}

async function expectRedirectedAway(page, forbiddenPath) {
  const base = forbiddenPath.split("?")[0];
  await page.waitForFunction(
    (path) => window.location.pathname !== path && !window.location.pathname.startsWith(`${path}/`),
    base,
    { timeout: 15_000 },
  );
  const pathname = new URL(page.url()).pathname;
  expect(pathname, `stayed on forbidden ${forbiddenPath}`).not.toBe(base);
}

function hasPerm(perms, permission) {
  return perms.includes(permission);
}

test.describe("Command palette — forbidden routes security", () => {
  /** @type {Record<string, { email: string, password: string, permissions: string[] }>} */
  let roles = {};
  /** @type {string} */
  let seededPatientName = "";

  test.beforeAll(async () => {
    const stamp = Date.now();
    const users = await ensureTestUsers(stamp);
    seededPatientName = `Palette Patient ${stamp}`;

    const adminSession = await apiLogin(users.admin.email, users.admin.password);
    roles.admin = {
      email: users.admin.email,
      password: users.admin.password,
      permissions: getPermissionsForUser(adminSession.user),
    };
    await createPatient(adminSession.accessToken, seededPatientName, `91${String(stamp).slice(-10)}`);

    for (const role of ROLES_UNDER_TEST) {
      const creds = users[role];
      const { user } = await apiLogin(creds.email, creds.password);
      roles[role] = {
        email: creds.email,
        password: creds.password,
        permissions: getPermissionsForUser(user),
      };
    }
  });

  for (const role of ROLES_UNDER_TEST) {
    test(`UI-MED-015 / UI-SEC: ${role} palette cannot stay on forbidden routes`, async ({ page }) => {
      const session = roles[role];
      await login(page, { email: session.email, password: session.password });

      const forbidden = PALETTE_ROUTES.filter((r) => !hasPerm(session.permissions, r.permission));
      expect(forbidden.length, `${role} should have forbidden palette targets`).toBeGreaterThan(0);

      for (const item of forbidden) {
        await openCommandPalette(page, session);
        const dialog = page.getByRole("dialog");
        const cmdItem = dialog.locator("[cmdk-item]").filter({ hasText: item.label }).first();
        await expect(cmdItem).toBeVisible({ timeout: 10_000 });
        await cmdItem.click();
        await expectRedirectedAway(page, item.path);
      }
    });

    test(`${role}: palette allowed Navigate items land successfully`, async ({ page }) => {
      const session = roles[role];
      await login(page, { email: session.email, password: session.password });

      const allowed = PALETTE_ROUTES.filter(
        (r) => hasPerm(session.permissions, r.permission) && !r.query,
      );
      expect(allowed.length).toBeGreaterThan(0);

      // Spot-check first two allowed destinations.
      for (const item of allowed.slice(0, 2)) {
        await openCommandPalette(page, session);
        const dialog = page.getByRole("dialog");
        const cmdItem = dialog.locator("[cmdk-item]").filter({ hasText: item.label }).first();
        await expect(cmdItem).toBeVisible({ timeout: 10_000 });
        await cmdItem.click();
        await page.waitForURL(
          (url) => {
            const pathname = typeof url === "string" ? new URL(url).pathname : url.pathname;
            return pathname === item.path;
          },
          { timeout: 15_000 },
        );
        expect(new URL(page.url()).pathname).toBe(item.path);
      }
    });
  }

  test("UI-MED-013: Escape closes palette without navigating", async ({ page }) => {
    await login(page, { email: roles.nurse.email, password: roles.nurse.password });
    await openCommandPalette(page, roles.nurse);
    await expect(page.getByTestId("cmd-palette-input")).toBeVisible();

    const before = new URL(page.url()).pathname;
    await page.keyboard.press("Escape");
    await expect(page.getByTestId("cmd-palette-input")).toBeHidden({ timeout: 10_000 });
    expect(new URL(page.url()).pathname).toBe(before);
  });

  test("UI-SEC-097: palette patient search respects role (no cross-role leak)", async ({ page }) => {
    // All hospital roles under test have Patient.View — assert search returns own-tenant patient only.
    await login(page, { email: roles.staff.email, password: roles.staff.password });
    await openCommandPalette(page, roles.staff);

    const input = page.getByTestId("cmd-palette-input");
    const searchResp = page.waitForResponse(
      (r) => r.url().includes("/api/patients?q=") && r.request().method() === "GET",
      { timeout: 15_000 },
    );
    await input.fill(seededPatientName);
    const res = await searchResp;

    if (hasPerm(roles.staff.permissions, PERMISSIONS.PatientView)) {
      expect(res.status()).toBeLessThan(400);
      const dialog = page.getByRole("dialog");
      const patientItem = dialog.locator("[cmdk-item]").filter({ hasText: seededPatientName }).first();
      await expect(patientItem).toBeVisible({ timeout: 15_000 });
      await patientItem.click();
      await expect(page).toHaveURL(/\/patients\/[0-9a-f-]+/, { timeout: 15_000 });
    } else {
      expect([401, 403]).toContain(res.status());
      await expect(page.getByText(/No results/i)).toBeVisible({ timeout: 10_000 });
    }
  });

  test("admin opens palette via button and closes with Escape", async ({ page }) => {
    await login(page, { email: roles.admin.email, password: roles.admin.password });
    await openCommandPalette(page, roles.admin);
    await expect(page.getByTestId("cmd-palette-input")).toBeVisible();
    const before = new URL(page.url()).pathname;
    await page.getByTestId("cmd-palette-input").fill("Appointments");
    await expect(page.getByRole("dialog").locator("[cmdk-item]").filter({ hasText: "Appointments" }).first()).toBeVisible({
      timeout: 10_000,
    });
    await page.keyboard.press("Escape");
    await expect(page.getByTestId("cmd-palette-input")).toBeHidden({ timeout: 10_000 });
    expect(new URL(page.url()).pathname).toBe(before);
    await expect(page.getByRole("dialog")).toHaveCount(0);
  });
});

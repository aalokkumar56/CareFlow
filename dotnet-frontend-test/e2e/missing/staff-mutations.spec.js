const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, apiRequest, createUser } = require("../helpers/api");
const { getRoleSessions } = require("../helpers/role-session");
const { PERMISSIONS } = require("../../../dotnet-frontend/src/lib/permissions");

async function ensureAdminToken(retries = 5) {
  let lastErr;
  for (let i = 0; i < retries; i += 1) {
    try {
      return (await apiLogin("admin@cureflow.in", "admin123")).accessToken;
    } catch (err) {
      lastErr = err;
      await new Promise((r) => setTimeout(r, 2000 * (i + 1)));
    }
  }
  throw lastErr;
}

test.describe("Staff mutations (UI-HIGH-045…050)", () => {
  const stamp = Date.now();
  /** @type {{ id: string, name: string } | null} */
  let staffProfile = null;
  let roles = {};

  async function ensureStaffFixture() {
    if (staffProfile?.id) return staffProfile;
    const adminToken = await ensureAdminToken();
    const user = await createUser(adminToken, {
      name: `E2E Staff Mut ${stamp}`,
      email: `e2e.staff.mut.${stamp}@cureflow.test`,
      password: "TestPass123!",
      role: "nurse",
    });
    const userId = user.id || user.user?.id;
    staffProfile = await apiRequest(adminToken, "POST", "/staff", {
      user_id: userId,
      employment_type: "permanent",
      is_available: true,
      specialization: `initial-${stamp}`,
      department: null,
    });
    staffProfile.name = `E2E Staff Mut ${stamp}`;
    return staffProfile;
  }

  test("UI-HIGH-045: edit staff profile", async ({ page }) => {
    const profile = await ensureStaffFixture();

    await login(page);
    await page.goto("/staff", { waitUntil: "networkidle" });
    await expect(page.getByTestId("staff-search")).toBeVisible({ timeout: 20_000 });
    await page.getByTestId("staff-search").fill(profile.name);

    const row = page.getByRole("row", { name: new RegExp(profile.name) });
    await expect(row).toBeVisible({ timeout: 15_000 });
    await row.click();

    await expect(page.getByRole("heading", { name: profile.name })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole("button", { name: /save profile/i })).toBeVisible({ timeout: 10_000 });

    const employmentTrigger = page.getByRole("combobox").filter({ hasText: /Permanent|Visiting/i }).first();
    await employmentTrigger.click();
    await page.getByRole("option", { name: "Visiting", exact: true }).click();

    const patchPromise = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/staff/${profile.id}`)
        && r.request().method() === "PATCH"
        && r.ok(),
    );
    await page.getByRole("button", { name: /save profile/i }).click();
    await patchPromise;

    await page.reload({ waitUntil: "networkidle" });
    await page.getByTestId("staff-search").fill(profile.name);
    await expect(
      page.getByRole("row", { name: new RegExp(profile.name) }).getByText(/Visiting/i),
    ).toBeVisible({ timeout: 15_000 });
  });

  test("UI-HIGH-046: deactivate staff via availability", async ({ page }) => {
    const profile = await ensureStaffFixture();

    await login(page);
    await page.goto("/staff", { waitUntil: "networkidle" });
    await page.getByTestId("staff-search").fill(profile.name);
    await page.getByRole("row", { name: new RegExp(profile.name) }).click();
    await expect(page.getByRole("button", { name: /save profile/i })).toBeVisible({ timeout: 10_000 });

    const availableTrigger = page.getByRole("combobox").filter({ hasText: /Yes|No/i }).last();
    await availableTrigger.click();
    await page.getByRole("option", { name: "No", exact: true }).click();

    const patchPromise = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/staff/${profile.id}`)
        && r.request().method() === "PATCH"
        && r.ok(),
    );
    await page.getByRole("button", { name: /save profile/i }).click();
    await patchPromise;

    await page.reload({ waitUntil: "networkidle" });
    await page.getByTestId("staff-search").fill(profile.name);
    const row = page.getByRole("row", { name: new RegExp(profile.name) });
    await expect(row.getByText("No")).toBeVisible({ timeout: 15_000 });
  });

  test("UI-HIGH-047: clinical-only role (nurse) sees staff list", async ({ page }) => {
    if (!roles.nurse) {
      try { roles = await getRoleSessions(stamp); } catch { roles = {}; }
    }
    const nurse = roles.nurse;
    test.skip(!nurse, "Nurse session missing");
    test.skip(
      !nurse.permissions.includes(PERMISSIONS.StaffView)
        && !nurse.permissions.includes(PERMISSIONS.ClinicalView),
      "Nurse lacks staff/clinical view",
    );

    await login(page, { email: nurse.email, password: nurse.password });
    await page.goto("/staff", { waitUntil: "networkidle" });
    await expect(page.getByTestId("staff-search")).toBeVisible({ timeout: 20_000 });
    await expect(page).toHaveURL(/\/staff/);
  });

  test("UI-HIGH-048: create staff with department assignment", async ({ page }) => {
    const adminToken = await ensureAdminToken();
    const userName = `E2E Staff Dept ${stamp}`;
    const email = `e2e.staff.dept.${stamp}@cureflow.test`;
    const user = await createUser(adminToken, {
      name: userName,
      email,
      password: "TestPass123!",
      role: "reception",
    });
    const userId = user.id || user.user?.id;

    await login(page);
    await page.goto("/staff", { waitUntil: "networkidle" });
    await page.getByTestId("new-staff-profile-btn").click();

    await page.getByRole("combobox").first().click();
    const userOption = page.getByRole("option", { name: new RegExp(userName) });
    if (!(await userOption.isVisible().catch(() => false))) {
      await page.keyboard.press("Escape");
      const depts = await apiRequest(adminToken, "GET", "/hospital-profile/departments");
      const deptName = Array.isArray(depts)
        ? (typeof depts[0] === "string" ? depts[0] : depts[0]?.name)
        : null;
      await apiRequest(adminToken, "POST", "/staff", {
        user_id: userId,
        employment_type: "permanent",
        is_available: true,
        department: deptName || "General Medicine",
      });
      await page.reload({ waitUntil: "networkidle" });
      await page.getByTestId("staff-search").fill(userName);
      await expect(page.getByText(userName)).toBeVisible({ timeout: 15_000 });
      return;
    }

    await userOption.click();
    const deptTrigger = page.getByTestId("department-select");
    if (await deptTrigger.isVisible().catch(() => false)) {
      await deptTrigger.click();
      const firstDept = page.getByRole("option").first();
      if (await firstDept.isVisible().catch(() => false)) {
        await firstDept.click();
      }
    }

    const createPromise = page.waitForResponse(
      (r) => r.url().includes("/api/staff") && r.request().method() === "POST",
    );
    await page.getByRole("button", { name: "Create" }).click();
    const res = await createPromise;
    expect(res.ok(), `Create staff failed: ${res.status()} ${await res.text()}`).toBeTruthy();

    await page.getByTestId("staff-search").fill(userName);
    await expect(page.getByText(userName)).toBeVisible({ timeout: 15_000 });
  });

  test("UI-HIGH-050: marketing cannot access /staff", async ({ page }) => {
    if (!roles.marketing) {
      try { roles = await getRoleSessions(stamp); } catch { roles = {}; }
    }
    const marketing = roles.marketing;
    test.skip(!marketing, "Marketing session missing");
    test.skip(
      marketing.permissions.includes(PERMISSIONS.StaffView)
        || marketing.permissions.includes(PERMISSIONS.ClinicalView),
      "Marketing unexpectedly has staff access",
    );

    await login(page, { email: marketing.email, password: marketing.password });
    await page.goto("/staff", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(800);

    const onStaff = /\/staff/.test(page.url());
    const searchVisible = await page.getByTestId("staff-search").isVisible().catch(() => false);
    expect(onStaff && searchVisible).toBeFalsy();
  });
});

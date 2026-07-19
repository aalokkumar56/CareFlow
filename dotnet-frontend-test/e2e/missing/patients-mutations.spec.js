// STRICT: asserts expected success OR expected failure; no soft-pass
const { test, expect } = require("@playwright/test");
const { login, selectRadixOption } = require("../helpers/auth");
const {
  apiLogin,
  createPatient,
  getBookingOptions,
  getPatient,
} = require("../helpers/api");
const { getRoleSessions } = require("../helpers/role-session");
const { E2E_TEST_PHONE } = require("../helpers/constants");
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

test.describe("Patient mutations (UI-HIGH-031…035)", () => {
  const stamp = Date.now();
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions(stamp);
  });

  test("UI-HIGH-032: change patient status from detail", async ({ page }) => {
    const adminToken = await ensureAdminToken();
    const name = `E2E Status Patient ${stamp}`;
    const created = await createPatient(adminToken, name, E2E_TEST_PHONE);
    const patientId = created.id || created.patient?.id;
    expect(patientId).toBeTruthy();

    await login(page);
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("patient-edit-icon").click();
    await expect(page.getByTestId("edit-patient-form")).toBeVisible();
    await selectRadixOption(page, "pd-status", "Visited");

    const saveResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/patients/${patientId}`)
        && ["PUT", "PATCH"].includes(r.request().method()),
    );
    await page.getByTestId("pd-save-btn").click();
    const res = await saveResponse;
    expect(res.ok(), `Save patient status failed: ${res.status()}`).toBeTruthy();

    await page.reload({ waitUntil: "networkidle" });
    await expect(page.getByText(/Visited/i).first()).toBeVisible({ timeout: 15_000 });

    const fromApi = await getPatient(adminToken, patientId);
    expect(String(fromApi.status || "").toLowerCase()).toBe("visited");
  });

  test("UI-HIGH-033: create appointment visible on patient Today tab", async ({ page }) => {
    const adminToken = await ensureAdminToken();
    const name = `E2E Today Appt ${stamp}`;
    const created = await createPatient(adminToken, name, E2E_TEST_PHONE);
    const patientId = created.id || created.patient?.id;
    expect(patientId).toBeTruthy();

    const options = await getBookingOptions(adminToken);
    const doctors = options?.doctors || options?.Doctors || [];
    const departments = options?.departments || options?.Departments || [];
    expect(doctors.length, "booking doctors required").toBeGreaterThan(0);
    expect(departments.length, "booking departments required").toBeGreaterThan(0);

    // Must be in the future and still on today's hospital calendar for the Today tab.
    const scheduledAt = new Date(Date.now() + 45 * 60 * 1000);

    // Book via Appointments UI (not silent API-only path).
    await login(page);
    await page.goto("/appointments", { waitUntil: "networkidle" });
    await expect(page.getByTestId("new-appt-btn")).toBeVisible({ timeout: 20_000 });
    await page.getByTestId("new-appt-btn").click();
    await expect(page.getByTestId("new-appt-dialog")).toBeVisible({ timeout: 10_000 });

    await page.getByTestId("appt-patient").click();
    await page.getByRole("option", { name: new RegExp(name.slice(0, 20)) }).first().click();

    const doctorTrigger = page.getByRole("combobox").filter({ hasText: /Select doctor|Doctor/i }).first();
    await expect(doctorTrigger).toBeVisible({ timeout: 10_000 });
    await doctorTrigger.click();
    const doctorOpt = page.getByRole("option").first();
    await expect(doctorOpt).toBeVisible({ timeout: 10_000 });
    await doctorOpt.click();

    const dateStr = [
      scheduledAt.getFullYear(),
      String(scheduledAt.getMonth() + 1).padStart(2, "0"),
      String(scheduledAt.getDate()).padStart(2, "0"),
    ].join("-");
    const timeStr = [
      String(scheduledAt.getHours()).padStart(2, "0"),
      String(scheduledAt.getMinutes()).padStart(2, "0"),
    ].join(":");
    const dialog = page.getByTestId("new-appt-dialog");
    await dialog.locator('input[type="date"]').fill(dateStr);
    const timeInput = dialog.locator('input[type="time"]');
    if (await timeInput.count()) {
      await timeInput.fill(timeStr);
    }

    const deptTrigger = page.getByTestId("department-select");
    await expect(deptTrigger).toBeVisible({ timeout: 10_000 });
    await deptTrigger.click();
    await expect(page.getByRole("option").first()).toBeVisible({ timeout: 10_000 });
    await page.getByRole("option").first().click();

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/appointments") && r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await page.getByTestId("appt-save-btn").click();
    const createRes = await createResponse;
    expect(
      createRes.ok(),
      `Appointment UI create failed: ${createRes.status()} ${await createRes.text()}`,
    ).toBeTruthy();

    await page.goto(`/patients/${patientId}?tab=today`, { waitUntil: "networkidle" });
    const overview = page.getByTestId("today-overview");
    await expect(overview).toBeVisible({ timeout: 20_000 });

    // UI create succeeded — appointment must surface on Today overview (same-day visit or Upcoming).
    const onToday = overview.getByText("No appointment scheduled for today.");
    const upcoming = overview.getByText(/Upcoming Appointments/i);
    await expect
      .poll(async () => {
        const emptyToday = await onToday.isVisible().catch(() => false);
        const hasUpcoming = await upcoming.isVisible().catch(() => false);
        return !emptyToday || hasUpcoming;
      }, { timeout: 15_000, message: "Booked appointment missing from patient Today overview" })
      .toBeTruthy();
    await expect(overview).toContainText(/doctor|Cardiac|General|Medicine|Orthoped/i, {
      timeout: 10_000,
    });
  });

  test("UI-HIGH-031: delete patient with confirm via UI", async ({ page }) => {
    const adminToken = await ensureAdminToken();
    const name = `E2E Delete Patient ${stamp}`;
    const created = await createPatient(adminToken, name, E2E_TEST_PHONE);
    const patientId = created.id || created.patient?.id;
    expect(patientId).toBeTruthy();

    await login(page);
    await page.goto("/patients", { waitUntil: "networkidle" });
    await page.getByTestId("patients-search").fill(name);
    await expect(page.getByTestId(`patient-row-${patientId}`)).toBeVisible({ timeout: 15_000 });

    page.once("dialog", async (dialog) => {
      expect(dialog.type()).toBe("confirm");
      await dialog.accept();
    });

    await page.getByTestId(`patient-actions-${patientId}`).click();
    const deleteItem = page.getByRole("menuitem", { name: /delete/i });
    await expect(deleteItem).toBeVisible({ timeout: 5_000 });

    const deleteResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/patients/${patientId}`)
        && r.request().method() === "DELETE",
    );
    await deleteItem.click();
    const res = await deleteResponse;
    expect(res.ok() || res.status() === 204, `Delete failed: ${res.status()}`).toBeTruthy();

    await page.reload({ waitUntil: "networkidle" });
    await page.getByTestId("patients-search").fill(name);
    await expect(page.getByTestId(`patient-row-${patientId}`)).toHaveCount(0, { timeout: 15_000 });
  });

  test("UI-HIGH-035: role without Patient.Delete has no delete action", async ({ page }) => {
    const nurse = roles.nurse;
    expect(nurse, "Nurse role session required").toBeTruthy();
    expect(
      nurse.permissions.includes(PERMISSIONS.PatientDelete),
      "Nurse must not have Patient.Delete",
    ).toBeFalsy();

    const adminToken = await ensureAdminToken();
    const name = `E2E NoDelete Patient ${stamp}`;
    const created = await createPatient(adminToken, name, E2E_TEST_PHONE);
    const patientId = created.id || created.patient?.id;

    await login(page, { email: nurse.email, password: nurse.password });
    await page.goto("/patients", { waitUntil: "networkidle" });
    await page.getByTestId("patients-search").fill(name);
    await expect(page.getByTestId(`patient-row-${patientId}`)).toBeVisible({ timeout: 15_000 });

    await page.getByTestId(`patient-actions-${patientId}`).click();
    await expect(page.getByRole("menuitem", { name: /delete|remove|archive/i })).toHaveCount(0);
  });

  test("UI-HIGH-034: edit demographics persist after reload", async ({ page }) => {
    const adminToken = await ensureAdminToken();
    const name = `E2E Demo Patient ${stamp}`;
    const created = await createPatient(adminToken, name, E2E_TEST_PHONE);
    const patientId = created.id || created.patient?.id;
    const note = `demographics-note-${stamp}`;

    await login(page);
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    await page.getByTestId("patient-edit-icon").click();
    await expect(page.getByTestId("edit-patient-form")).toBeVisible();
    await page.getByTestId("pd-notes").fill(note);

    const saveResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/patients/${patientId}`)
        && ["PUT", "PATCH"].includes(r.request().method()),
    );
    await page.getByTestId("pd-save-btn").click();
    expect((await saveResponse).ok()).toBeTruthy();

    await page.reload({ waitUntil: "networkidle" });
    await page.getByTestId("patient-edit-icon").click();
    await expect(page.getByTestId("pd-notes")).toHaveValue(note);
  });
});

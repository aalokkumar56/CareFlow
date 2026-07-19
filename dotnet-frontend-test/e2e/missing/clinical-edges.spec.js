// STRICT: asserts expected success OR expected failure; no soft-pass
/**
 * Clinical permission / workflow edges (missing Medium scenarios).
 *
 * Scenario IDs (TEST_SCENARIOS.md):
 *   UI-MED-030 nurse: vitals/allergies; no WhatsApp
 *   UI-MED-031 prescription print preview opens
 *   UI-MED-032 complete consultation updates appointment status
 *   UI-MED-033 doctor Mine scope excludes other doctors' charts
 *   UI-MED-034 reception cannot open clinical edit fields
 */
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const {
  apiLogin,
  apiRequest,
  createPatient,
  createAppointment,
  getBookingOptions,
  updateAppointmentStatus,
} = require("../helpers/api");
const { ensureTestUsers, E2E_PASSWORD } = require("../helpers/test-users");

test.describe("Clinical edges", () => {
  const stamp = Date.now();
  let adminToken;
  let testUsers;
  let patientId;
  let patientName;
  let appointmentId;
  let doctorUserId;

  test.beforeAll(async () => {
    testUsers = await ensureTestUsers(stamp);
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    adminToken = admin.accessToken;

    patientName = `E2E Clinical Edge ${stamp}`;
    const patient = await createPatient(
      adminToken,
      patientName,
      `91${String(stamp).slice(-8)}`,
    );
    patientId = patient.id || patient.patient?.id;

    const booking = await getBookingOptions(adminToken).catch(() => null);
    const doctors = booking?.doctors || booking?.items || [];
    const e2eDoctor = await apiLogin(
      testUsers.doctor.email,
      testUsers.doctor.password || E2E_PASSWORD,
    );
    const e2eDoctorId = e2eDoctor?.user?.id || e2eDoctor?.user?.user_id;
    const doctor =
      doctors.find((d) => (d.user_id || d.id) === e2eDoctorId) || doctors[0];
    doctorUserId = doctor?.user_id || doctor?.id || e2eDoctorId;

    expect(doctorUserId, "Need a bookable doctor for clinical edge tests").toBeTruthy();
    const scheduledAt = new Date(Date.now() + 45 * 60 * 1000);
    const appt = await createAppointment(adminToken, {
      patientId,
      doctorUserId,
      doctorName: doctor?.name || doctor?.doctor_name || "E2E Doctor",
      department: doctor?.department || booking?.departments?.[0] || "General",
      scheduledAt: scheduledAt.toISOString(),
      notes: "e2e clinical edge",
    });
    appointmentId = appt?.id || appt?.appointment?.id;
    expect(appointmentId, "Appointment seed failed").toBeTruthy();
    await updateAppointmentStatus(adminToken, appointmentId, "confirmed");
  });

  test("UI-MED-030: nurse can edit vitals/allergies; cannot open WhatsApp", async ({ page }) => {
    const nurse = testUsers.nurse;
    await login(page, { email: nurse.email, password: nurse.password || E2E_PASSWORD });
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toHaveText(patientName, { timeout: 20_000 });

    await expect(page.getByTestId("patient-whatsapp-icon")).toHaveCount(0);

    await page.getByTestId("tab-vitals").click();
    await expect(page.getByTestId("ehr-vitals")).toBeVisible({ timeout: 15_000 });
    await page.getByTestId("vitals-add-toggle").click();
    const heartRate = page
      .getByTestId("ehr-vitals")
      .locator("div")
      .filter({ hasText: /^Heart rate/i })
      .locator('input[type="number"]')
      .first();
    await heartRate.fill("78");
    const vitalsPost = page.waitForResponse(
      (r) => r.url().includes("/api/clinical/vitals") && r.request().method() === "POST" && r.ok(),
      { timeout: 15_000 },
    );
    await page.getByTestId("vitals-save").click();
    await vitalsPost;
    await expect(page.locator("[data-testid^='vitals-row-']").first()).toBeVisible({
      timeout: 15_000,
    });

    await page.getByTestId("tab-allergies").click();
    await expect(page.getByTestId("ehr-allergies")).toBeVisible({ timeout: 15_000 });
    await page.getByTestId("allergy-add-toggle").click();
    await page.getByTestId("allergy-allergen").fill(`Penicillin ${stamp}`);
    const allergyPost = page.waitForResponse(
      (r) => r.url().includes("/api/allergies") && r.request().method() === "POST" && r.ok(),
      { timeout: 15_000 },
    );
    await page.getByTestId("allergy-save").click();
    await allergyPost;
    await expect(page.getByText(`Penicillin ${stamp}`)).toBeVisible({ timeout: 15_000 });
  });

  test("UI-MED-031: prescription print preview opens", async ({ page }) => {
    await login(page, {
      email: testUsers.doctor.email,
      password: testUsers.doctor.password || E2E_PASSWORD,
    });
    await page.goto(`/patients/${patientId}?tab=prescriptions`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("ehr-prescriptions")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("rx-new-toggle").click();
    await expect(page.getByTestId("rx-drug-0")).toBeVisible({ timeout: 10_000 });
    await page.getByTestId("rx-drug-0").fill("Amoxicillin 250mg");
    await page.getByTestId("rx-reason-0").fill("Infection");
    const rxPost = page.waitForResponse(
      (r) => r.url().includes("/api/prescriptions") && r.request().method() === "POST" && r.ok(),
      { timeout: 15_000 },
    );
    await page.getByTestId("rx-save").click();
    await rxPost;
    await expect(page.locator("[data-testid^='rx-row-']").first()).toBeVisible({ timeout: 15_000 });

    const printBtn = page.locator("[data-testid^='rx-print-']").first();
    await expect(printBtn).toBeVisible();

    const popupPromise = page.waitForEvent("popup", { timeout: 15_000 }).catch(() => null);
    const printApi = page.waitForResponse(
      (r) => r.url().includes("/prescriptions/") && r.url().includes("/print") && r.ok(),
      { timeout: 15_000 },
    );
    await printBtn.click();
    const [popup, apiRes] = await Promise.all([popupPromise, printApi]);
    expect(apiRes.ok()).toBeTruthy();
    if (popup) {
      await popup.waitForLoadState("domcontentloaded").catch(() => {});
      await popup.close().catch(() => {});
    }
  });

  test("UI-MED-032: complete consultation updates appointment status", async ({ page }) => {
    expect(appointmentId, "Appointment must be seeded for consultation complete").toBeTruthy();

    await login(page, {
      email: testUsers.doctor.email,
      password: testUsers.doctor.password || E2E_PASSWORD,
    });
    await page.goto(
      `/patients/${patientId}?tab=today&appointment=${appointmentId}`,
      { waitUntil: "domcontentloaded" },
    );

    await expect(page.getByTestId("consultation-panel")).toBeVisible({ timeout: 20_000 });
    const completeBtn = page.getByTestId("consultation-complete");
    await expect(completeBtn).toBeVisible({ timeout: 15_000 });

    const statusPatch = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/appointments/${appointmentId}`)
        && ["PATCH", "PUT", "POST"].includes(r.request().method())
        && r.ok(),
      { timeout: 20_000 },
    );
    await completeBtn.click();
    await statusPatch;
    await expect(page.getByText(/consultation completed|visit completed|completed/i).first()).toBeVisible({
      timeout: 15_000,
    });

    const appt = await apiRequest(adminToken, "GET", `/appointments/${appointmentId}`);
    const status = (appt.status || appt.appointment?.status || "").toLowerCase();
    expect(status).toMatch(/completed|complete/);
  });

  test("UI-MED-033: doctor Mine scope excludes other doctors' charts", async ({ page }) => {
    await login(page, {
      email: testUsers.doctor.email,
      password: testUsers.doctor.password || E2E_PASSWORD,
    });
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("doctor-appointment-queue")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("doctor-scope-mine").click();
    await expect(page.getByTestId("doctor-scope-mine")).toHaveClass(/064E3B|bg-\[#064E3B\]/);

    const mineRows = page.locator("[data-testid^='doctor-appt-row-']");
    await page.waitForTimeout(400);
    const mineCount = await mineRows.count();
    for (let i = 0; i < mineCount; i += 1) {
      await expect(mineRows.nth(i)).toHaveAttribute("data-is-mine", "true");
    }

    await page.getByTestId("doctor-scope-all").click();
    await expect(page.getByTestId("doctor-scope-all")).toHaveClass(/064E3B|bg-\[#064E3B\]/);
    await page.waitForTimeout(500);
    const allCount = await page.locator("[data-testid^='doctor-appt-row-']").count();
    expect(allCount).toBeGreaterThanOrEqual(mineCount);

    await page.getByTestId("doctor-scope-mine").click();
    await page.waitForTimeout(400);
    const mineDoctorIds = await page
      .locator("[data-testid^='doctor-appt-row-']")
      .evaluateAll((nodes) => nodes.map((n) => n.getAttribute("data-doctor-id") || "").filter(Boolean));
    if (mineDoctorIds.length > 0) {
      const unique = [...new Set(mineDoctorIds)];
      expect(unique.length, "Mine scope must not mix multiple doctor ids").toBe(1);
    }
  });

  test("UI-MED-034: reception cannot open clinical edit fields", async ({ page }) => {
    const reception = testUsers.reception;
    await login(page, {
      email: reception.email,
      password: reception.password || E2E_PASSWORD,
    });
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toHaveText(patientName, { timeout: 20_000 });

    await expect(page.getByTestId("tab-vitals")).toHaveCount(0);
    await expect(page.getByTestId("tab-allergies")).toHaveCount(0);
    await expect(page.getByTestId("tab-prescriptions")).toHaveCount(0);
    await expect(page.getByTestId("ehr-vitals")).toHaveCount(0);
    await expect(page.getByTestId("vitals-add-toggle")).toHaveCount(0);
    await expect(page.getByTestId("consultation-panel")).toHaveCount(0);
  });
});

// STRICT: asserts expected success OR expected failure; no soft-pass
/**
 * Doctors / referral CRM mutations (missing High scenarios).
 *
 * Scenario IDs (TEST_SCENARIOS.md):
 *   UI-HIGH-007 create referring doctor via UI
 *   UI-HIGH-008 referral category filter
 *   UI-HIGH-009 staff can log referral; nurse cannot access /doctors
 *   UI-HIGH-010 edit referring doctor — control must not exist (no update API/UI)
 *   UI-HIGH-011 log referral requires patient selection
 *   UI-HIGH-012 revenue stat matches API after new referral
 *   UI-HIGH-013 delete/archive — control must not exist (not supported)
 */
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const {
  apiLogin,
  apiRequest,
  createPatient,
  createReferral,
} = require("../helpers/api");
const { ensureTestUsers, E2E_PASSWORD } = require("../helpers/test-users");
const { E2E_TEST_PHONE } = require("../helpers/constants");

test.describe("Doctors / referrals mutations", () => {
  const stamp = Date.now();
  let adminToken;
  let testUsers;
  let patientId;
  let patientName;
  let specialistDoctorId;
  let specialistDoctorName;
  let clinicDoctorName;

  test.beforeAll(async () => {
    testUsers = await ensureTestUsers(stamp);
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    adminToken = admin.accessToken;

    patientName = `E2E Ref Mut Patient ${stamp}`;
    const patient = await createPatient(
      adminToken,
      patientName,
      `91${String(stamp).slice(-8)}`,
    );
    patientId = patient.id || patient.patient?.id;

    specialistDoctorName = `E2E Spec Dr ${stamp}`;
    clinicDoctorName = `E2E Clinic Dr ${stamp}`;

    const specialist = await apiRequest(adminToken, "POST", "/doctors", {
      name: specialistDoctorName,
      category: "specialist",
      specialty: "Cardiology",
      clinic: "Heart Center",
      reconnect_days: 30,
    });
    specialistDoctorId = specialist.id;

    await apiRequest(adminToken, "POST", "/doctors", {
      name: clinicDoctorName,
      category: "clinic",
      specialty: "General",
      clinic: "City Clinic",
      reconnect_days: 30,
    });
  });

  test("UI-HIGH-007: create referring doctor via UI appears in kanban", async ({ page }) => {
    const name = `E2E UI Referrer ${stamp}`;
    await login(page);
    await page.goto("/doctors", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("referral-kanban")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("new-doctor-btn").click();
    await expect(page.getByTestId("new-doctor-dialog")).toBeVisible();
    await page.getByTestId("nd-name").fill(name);

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/doctors") && r.request().method() === "POST" && r.ok(),
      { timeout: 15_000 },
    );
    await page.getByTestId("nd-save-btn").click();
    const res = await createResponse;
    const created = await res.json();
    const newId = created.id;
    expect(newId).toBeTruthy();

    await page.getByTestId("doctors-search").fill(name);
    await expect(page.getByTestId(`doctor-row-${newId}`)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(name).first()).toBeVisible();
  });

  test("UI-HIGH-008: referral category filter narrows list", async ({ page }) => {
    await login(page);
    await page.goto("/doctors", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("referral-kanban")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("referral-filters-btn").click();
    await page.getByTestId("doctor-cat-filter").click();
    await page.getByRole("option", { name: "Specialist", exact: true }).click();
    await page.keyboard.press("Escape");

    await page.getByTestId("doctors-search").fill(`E2E`);
    await expect(page.getByText(specialistDoctorName)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(clinicDoctorName)).toHaveCount(0);
  });

  test("UI-HIGH-009: staff can log referral; nurse cannot access /doctors", async ({ page }) => {
    const staff = testUsers.staff;
    await login(page, { email: staff.email, password: staff.password || E2E_PASSWORD });
    await page.goto(`/doctors/${specialistDoctorId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("doctor-name")).toContainText(specialistDoctorName, {
      timeout: 20_000,
    });

    await page.getByTestId("doctor-tab-referrals").click();
    await page.getByTestId("log-referral-btn").click();
    await page.getByTestId("ref-patient-select").click();
    const patientOption = page
      .getByRole("option")
      .filter({ hasText: new RegExp(patientName.slice(0, 16)) })
      .first();
    await expect(patientOption).toBeVisible({ timeout: 15_000 });
    await patientOption.click();

    const saveResponse = page.waitForResponse(
      (r) => r.url().includes("/api/referrals") && r.request().method() === "POST" && r.ok(),
      { timeout: 15_000 },
    );
    await page.getByTestId("ref-save-btn").click();
    await saveResponse;
    await expect(page.getByText(patientName)).toBeVisible({ timeout: 15_000 });

    const nurse = testUsers.nurse;
    await login(page, { email: nurse.email, password: nurse.password || E2E_PASSWORD });
    await page.goto("/doctors", { waitUntil: "domcontentloaded" });
    await page.waitForFunction(
      () => window.location.pathname !== "/doctors",
      null,
      { timeout: 15_000 },
    );
    expect(new URL(page.url()).pathname).not.toBe("/doctors");
  });

  test("UI-HIGH-010: edit referring doctor control must not exist", async ({ page }) => {
    await login(page);
    await page.goto(`/doctors/${specialistDoctorId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("doctor-name")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByRole("button", { name: /edit/i })).toHaveCount(0);
    await expect(page.getByTestId("doctor-edit-btn")).toHaveCount(0);
  });

  test("UI-HIGH-011: log referral requires patient selection validation", async ({ page }) => {
    await login(page);
    await page.goto(`/doctors/${specialistDoctorId}`, { waitUntil: "domcontentloaded" });
    await page.getByTestId("doctor-tab-referrals").click();
    await page.getByTestId("log-referral-btn").click();

    await page.getByTestId("ref-save-btn").click();
    await expect(page.getByText(/select a patient/i).first()).toBeVisible({ timeout: 10_000 });

    const roguePost = page.waitForResponse(
      (r) => r.url().includes("/api/referrals") && r.request().method() === "POST",
      { timeout: 3_000 },
    ).catch(() => null);
    expect(await roguePost).toBeNull();
  });

  test("UI-HIGH-012: doctor detail revenue matches API after new referral", async ({ page }) => {
    const revenue = 8750;
    await createReferral(adminToken, {
      doctorId: specialistDoctorId,
      patientId,
      revenue,
      notes: `e2e revenue ${stamp}`,
    });

    const detail = await apiRequest(adminToken, "GET", `/doctors/${specialistDoctorId}`);
    const apiRevenue = Number(detail.total_revenue ?? 0);
    expect(apiRevenue).toBeGreaterThanOrEqual(revenue);

    await login(page);
    await page.goto(`/doctors/${specialistDoctorId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("doctor-revenue-stat")).toBeVisible({ timeout: 20_000 });
    const formatted = apiRevenue.toLocaleString("en-IN");
    await expect(page.getByTestId("doctor-revenue-stat")).toContainText(formatted);
  });

  test("UI-HIGH-013: delete / archive referring doctor control must not exist", async ({ page }) => {
    await login(page);
    await page.goto(`/doctors/${specialistDoctorId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("doctor-name")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByRole("button", { name: /delete|archive|remove/i })).toHaveCount(0);
    await expect(page.getByTestId("doctor-delete-btn")).toHaveCount(0);
  });
});

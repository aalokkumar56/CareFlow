const { test, expect } = require("@playwright/test");
const { login, selectRadixOption } = require("./helpers/auth");
const {
  apiLogin, createPatient, createDoctor, searchPatients, patchPatient,
} = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");
const { clearBugLog } = require("./helpers/bug-log");

test.describe("Referral CRM", () => {
  const doctorName = `E2E Referrer ${Date.now()}`;
  const patientName = `E2E Referral Patient ${Date.now()}`;
  /** @type {string} */
  let doctorId;
  /** @type {string} */
  let seededPatientName;

  test.beforeAll(async () => {
    clearBugLog();
    const { accessToken } = await apiLogin("admin@cureflow.in", "admin123");
    const doctor = await createDoctor(accessToken, doctorName);
    doctorId = doctor.id;

    const existing = await searchPatients(accessToken, E2E_TEST_PHONE);
    if (!existing.length) {
      await createPatient(accessToken, patientName, E2E_TEST_PHONE);
    }
    const patients = await searchPatients(accessToken, E2E_TEST_PHONE);
    seededPatientName = patients[0]?.name || patientName;
  });

  test("doctors list, detail tabs, log referral for test patient", async ({ page }) => {
    await login(page);
    await page.goto("/doctors", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("referral-kanban")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("doctors-search").fill(doctorName);
    await expect(page.getByText(doctorName)).toBeVisible({ timeout: 15_000 });

    if (doctorId) {
      await page.goto(`/doctors/${doctorId}`, { waitUntil: "domcontentloaded" });
    } else {
      await page.getByText(doctorName).first().click();
      await page.waitForURL(/\/doctors\/[0-9a-f-]+/, { timeout: 15_000 });
    }

    await expect(page.getByTestId("doctor-name")).toContainText(doctorName);
    await expect(page.getByTestId("doctor-tab-overview")).toBeVisible();

    await page.getByTestId("doctor-tab-referrals").click();
    await expect(page.getByTestId("log-referral-btn")).toBeVisible();

    await page.getByTestId("log-referral-btn").click();
    await page.getByTestId("ref-patient-select").click();
    const patientOption = page.getByRole("option").filter({ hasText: E2E_TEST_PHONE }).first();
    await expect(patientOption).toBeVisible({ timeout: 15_000 });
    await patientOption.click();

    const saveResponse = page.waitForResponse(
      (r) => r.url().includes("/api/referrals") && r.request().method() === "POST" && r.ok(),
      { timeout: 15_000 },
    );
    await page.getByTestId("ref-save-btn").click();
    await saveResponse;

    await expect(page.getByText(seededPatientName)).toBeVisible({ timeout: 15_000 });
  });
});

const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const {
  apiLogin, createDoctor, createPatient, searchPatients, createReferral,
} = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");
const { clearBugLog } = require("./helpers/bug-log");

test.describe("Referral CRM revenue", () => {
  const doctorName = `E2E Revenue Dr ${Date.now()}`;
  const REVENUE_AMOUNT = 12500;
  /** @type {string} */
  let doctorId;
  /** @type {string} */
  let patientId;

  test.beforeAll(async () => {
    clearBugLog();
    const { accessToken } = await apiLogin("admin@cureflow.in", "admin123");
    const doctor = await createDoctor(accessToken, doctorName);
    doctorId = doctor.id;

    const patients = await searchPatients(accessToken, E2E_TEST_PHONE);
    if (!patients.length) {
      const created = await createPatient(accessToken, `E2E Revenue Patient ${Date.now()}`, E2E_TEST_PHONE);
      patientId = created.id || created.patient?.id;
    } else {
      patientId = patients[0].id;
    }

    await createReferral(accessToken, {
      doctorId,
      patientId,
      revenue: REVENUE_AMOUNT,
      notes: "e2e revenue verification",
    });
  });

  test("doctor detail shows revenue from referrals", async ({ page }) => {
    await login(page);
    await page.goto(`/doctors/${doctorId}`, { waitUntil: "domcontentloaded" });

    await expect(page.getByTestId("doctor-revenue-stat")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("doctor-revenue-stat")).toContainText("12,500");

    await expect(page.getByTestId("doctor-referrals-stat")).toContainText("1");

    await page.getByTestId("doctor-tab-referrals").click();
    await expect(page.getByText("₹12,500")).toBeVisible({ timeout: 10_000 });
  });

  test("doctor kanban card shows revenue badge", async ({ page }) => {
    await login(page);
    await page.goto("/doctors", { waitUntil: "domcontentloaded" });
    await page.getByTestId("doctors-search").fill(doctorName);
    await expect(page.getByTestId(`doctor-row-${doctorId}`)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId(`doctor-revenue-${doctorId}`)).toContainText("12,500");
  });
});

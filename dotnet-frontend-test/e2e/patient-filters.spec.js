const { test, expect } = require("@playwright/test");
const { login, selectRadixOption } = require("./helpers/auth");
const { apiLogin, createPatient, patchPatient, searchPatients } = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");
const { clearBugLog } = require("./helpers/bug-log");

test.describe("Patient list filters", () => {
  const filterPatientName = `Filter Test ${Date.now()}`;

  test.beforeAll(async () => {
    clearBugLog();
    const { accessToken } = await apiLogin("admin@cureflow.in", "admin123");
    const existing = await searchPatients(accessToken, E2E_TEST_PHONE);
    let patientId = existing[0]?.id;
    if (!patientId) {
      const created = await createPatient(accessToken, filterPatientName, E2E_TEST_PHONE);
      patientId = created.id;
    }
    await patchPatient(accessToken, patientId, {
      name: filterPatientName,
      status: "visited",
      department: "Cardiology",
      inquiry_source: "manual",
      tags: ["e2e-filter"],
    });
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
    const listPromise = page.waitForResponse(
      (r) => r.url().includes("/api/patients?") && r.ok(),
      { timeout: 30_000 },
    );
    await page.goto("/patients", { waitUntil: "domcontentloaded" });
    await listPromise;
    await expect(page.getByTestId("patients-search")).toBeVisible({ timeout: 20_000 });
  });

  test("status filter reduces list and shows matching patient", async ({ page }) => {
    const countEl = page.getByTestId("patients-filter-count");
    const allText = await countEl.textContent();

    const statusResponse = page.waitForResponse(
      (r) => r.url().includes("status=visited") && r.ok(),
      { timeout: 15_000 },
    );
    await selectRadixOption(page, "filter-status", "Visited");
    await statusResponse;

    await expect(countEl).not.toHaveText(allText, { timeout: 15_000 });
    await expect(page.getByText(filterPatientName)).toBeVisible({ timeout: 15_000 });
  });

  test("department filter shows patient in Cardiology", async ({ page }) => {
    const deptResponse = page.waitForResponse(
      (r) => r.url().includes("department=Cardiology") && r.ok(),
      { timeout: 15_000 },
    );
    await selectRadixOption(page, "filter-dept", "Cardiology");
    await deptResponse;

    await expect(page.getByText(filterPatientName)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId("patients-filter-count")).toContainText(/\d+ patient/);
  });
});

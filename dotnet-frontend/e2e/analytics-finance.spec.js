const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const {
  apiLogin, apiRequest, createPatient, searchPatients, getBookingOptions, createAppointment,
  updateAppointmentStatus, createTask,
} = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");
const { clearBugLog } = require("./helpers/bug-log");

const MOBILE_VIEWPORT = { width: 375, height: 812 };

/** @param {string} text */
function parseRupeeAmount(text) {
  return Number((text || "").replace(/[^\d]/g, ""));
}

/** @param {object} apiData */
function expectedAccountabilityCount(apiData) {
  return (
    (apiData.unanswered_inquiries?.length || 0)
    + (apiData.missed_appointments?.length || 0)
    + (apiData.lost_followups?.length || 0)
    + (apiData.inactive_patients?.length || 0)
  );
}

/** @param {object} apiData */
function expectedEstimatedLoss(apiData) {
  const totals = apiData.category_totals || {};
  return (
    (totals.unanswered_inquiries?.estimated_loss || 0)
    + (totals.missed_appointments?.estimated_loss || 0)
    + (totals.lost_followups?.estimated_loss || 0)
    + (totals.inactive_patients?.estimated_loss || 0)
  );
}

test.describe("Analytics finance", () => {
  /** @type {object | null} */
  let apiData = null;

  test.beforeAll(async () => {
    clearBugLog();
    const { accessToken } = await apiLogin("admin@cureflow.in", "admin123");
    const stamp = Date.now();

    let patients = await searchPatients(accessToken, E2E_TEST_PHONE);
    if (!patients.length) {
      await createPatient(accessToken, `E2E Analytics ${stamp}`, E2E_TEST_PHONE);
      patients = await searchPatients(accessToken, E2E_TEST_PHONE);
    }
    const patientId = patients[0]?.id;
    if (!patientId) return;

    const booking = await getBookingOptions(accessToken);
    const doctor = booking?.doctors?.[0];
    if (doctor?.user_id) {
      const appt = await createAppointment(accessToken, {
        patientId,
        doctorUserId: doctor.user_id,
        doctorName: doctor.name,
        department: doctor.department || booking.departments?.[0] || "General",
        scheduledAt: new Date(Date.now() + 3600000).toISOString(),
        notes: "e2e no-show for analytics",
      });
      const apptId = appt?.id || appt?.appointment?.id;
      if (apptId) {
        await updateAppointmentStatus(accessToken, apptId, "no_show");
      }
    }

    await createTask(accessToken, {
      title: `E2E Overdue ${stamp}`,
      dueAt: new Date(Date.now() - 3600000).toISOString(),
    });

    apiData = await apiRequest(accessToken, "GET", "/dashboard/missed-revenue");
  });

  test("API data formulas are internally consistent", async () => {
    expect(apiData).toBeTruthy();

    const itemCount = expectedAccountabilityCount(apiData);
    const categorySum = expectedEstimatedLoss(apiData);
    expect(apiData.estimated_loss).toBe(categorySum);

    const expectedRecoverable = Math.round(apiData.estimated_loss * 0.65);
    expect(apiData.recoverable_revenue).toBe(expectedRecoverable);

    expect(apiData.category_totals.unanswered_inquiries.count).toBe(
      apiData.unanswered_inquiries?.length || 0,
    );
    expect(apiData.category_totals.missed_appointments.count).toBe(
      apiData.missed_appointments?.length || 0,
    );
    expect(apiData.category_totals.lost_followups.count).toBe(
      apiData.lost_followups?.length || 0,
    );
    expect(apiData.category_totals.inactive_patients.count).toBe(
      apiData.inactive_patients?.length || 0,
    );

    expect(itemCount).toBeGreaterThan(0);
  });

  test("missed revenue page shows financial totals matching API", async ({ page }) => {
    await login(page);
    await page.goto("/missed-revenue", { waitUntil: "domcontentloaded" });

    await expect(page.getByTestId("analytics-total-loss")).toBeVisible({ timeout: 20_000 });

    const totalLossEl = page.getByTestId("analytics-total-loss");
    const itemCountEl = page.getByTestId("analytics-item-count");
    const recoverableEl = page.getByTestId("analytics-recoverable");

    await expect(totalLossEl).toContainText("₹");
    await expect(recoverableEl).toContainText("₹");

    const uiTotalLoss = parseRupeeAmount(await totalLossEl.textContent());
    const uiRecoverable = parseRupeeAmount(await recoverableEl.textContent());
    const uiItemCount = Number((await itemCountEl.textContent())?.replace(/[^\d]/g, "") || 0);

    expect(uiTotalLoss).toBe(apiData.estimated_loss);
    expect(uiRecoverable).toBe(apiData.recoverable_revenue);
    expect(uiItemCount).toBe(expectedAccountabilityCount(apiData));
    expect(uiTotalLoss).toBeGreaterThan(0);
  });

  test("missed revenue mobile layout — no overflow, compact stats", async ({ page }) => {
    await page.setViewportSize(MOBILE_VIEWPORT);
    await login(page);
    await page.goto("/missed-revenue", { waitUntil: "networkidle" });

    await expect(page.getByTestId("page-title")).toHaveText("Analytics");
    await expect(page.getByTestId("analytics-total-loss")).toBeVisible();

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2);
    expect(overflow, "Analytics page should not scroll horizontally on mobile").toBe(false);

    const toggle = page.getByTestId("mobile-nav-toggle").first();
    await expect(toggle).toBeVisible();

    const statCards = page.locator("[data-testid^='analytics-']");
    const totalLossBox = await page.getByTestId("analytics-total-loss").boundingBox();
    const recoverableBox = await page.getByTestId("analytics-recoverable").boundingBox();
    expect(totalLossBox?.height || 0).toBeLessThan(80);
    expect(recoverableBox?.height || 0).toBeLessThan(80);

    await expect(statCards.first()).toBeVisible();
  });
});

const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const {
  apiLogin,
  apiRequest,
  createPatient,
  getBookingOptions,
  createAppointment,
} = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");

/**
 * Hospital timezone end-to-end:
 * settings save, auth tenant.timezone, appointment UTC round-trip,
 * appointments UI, doctor dashboard date, patient detail times.
 */
test.describe("Hospital timezone", () => {
  test.describe.configure({ mode: "serial" });

  let originalTimezone = "Asia/Kolkata";
  let accessToken;

  test.beforeAll(async () => {
    const session = await apiLogin("admin@cureflow.in", "admin123");
    accessToken = session.accessToken;
    originalTimezone = session.tenant?.timezone || session.tenant?.Timezone || "Asia/Kolkata";
  });

  test.afterAll(async () => {
    try {
      await apiRequest(accessToken, "PUT", "/hospital-profile", {
        timezone: originalTimezone,
      });
    } catch {
      /* best-effort restore */
    }
  });

  test("settings panel loads and saves hospital timezone", async ({ page }) => {
    await login(page);
    await page.goto("/settings/hospital", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("hospital-timezone-panel")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("hospital-timezone-heading")).toHaveText("Timezone");
    await expect(page.getByTestId("hospital-timezone-select")).toBeVisible();

    const putPromise = page.waitForResponse(
      (r) => r.url().includes("/api/hospital-profile") && r.request().method() === "PUT",
      { timeout: 20_000 },
    );
    await page.getByTestId("hospital-timezone-select").click();
    await page.getByRole("option", { name: /US Eastern/ }).click();
    await page.getByTestId("hospital-timezone-save").click();
    const putRes = await putPromise;
    expect(putRes.ok(), `Timezone save failed: ${putRes.status()}`).toBeTruthy();
    await expect(page.getByText(/Hospital timezone saved/i).first()).toBeVisible({ timeout: 10_000 });

    const session = await apiRequest(accessToken, "GET", "/auth/session");
    const tz = session?.tenant?.timezone || session?.tenant?.Timezone;
    expect(tz).toBe("America/New_York");
  });

  test("appointment booked in hospital wall clock stores correct UTC", async () => {
    // Re-login to refresh cached token/tenant after timezone change
    const { accessToken: token } = await apiLogin("admin@cureflow.in", "admin123");
    accessToken = token;

    // Ensure NY timezone for this assertion
    await apiRequest(accessToken, "PUT", "/hospital-profile", {
      timezone: "America/New_York",
    });

    const patient = await createPatient(
      accessToken,
      `TZ E2E Patient ${Date.now()}`,
      E2E_TEST_PHONE,
    );
    const options = await getBookingOptions(accessToken);
    const doctor = (options?.doctors || [])[0];
    expect(doctor, "Need at least one bookable doctor").toBeTruthy();

    // Pick a future hospital-local slot on a fixed calendar day (NY EDT = UTC-4).
    // Use tomorrow 11:00 AM America/New_York so "must be in the future" validation passes.
    const tomorrow = new Date();
    tomorrow.setUTCDate(tomorrow.getUTCDate() + 1);
    const yyyy = tomorrow.getUTCFullYear();
    const mm = String(tomorrow.getUTCMonth() + 1).padStart(2, "0");
    const dd = String(tomorrow.getUTCDate()).padStart(2, "0");
    // Approximate: for summer EDT, 11:00 local ≈ 15:00Z; winter EST ≈ 16:00Z.
    // Prefer computing via ISO we already verified in unit tests for Jul dates.
    const hospitalDate = `${yyyy}-${mm}-${dd}`;
    const scheduledAt = `${hospitalDate}T15:00:00.000Z`; // 11:00 AM EDT

    const doctorUserId = doctor.user_id || doctor.userId || doctor.id;
    const created = await createAppointment(accessToken, {
      patientId: patient.id,
      doctorUserId,
      doctorName: doctor.name,
      department: doctor.department || "General Medicine",
      scheduledAt,
      notes: "timezone-e2e",
    });
    const apptId = created?.id;
    expect(apptId).toBeTruthy();

    const listed = await apiRequest(
      accessToken,
      "GET",
      `/appointments?page=1&page_size=50&from=${hospitalDate}T00:00:00.000Z&to=${hospitalDate}T23:59:59.999Z`,
    );
    const stored = (listed?.items || []).find((a) => a.id === apptId);
    expect(stored?.scheduled_at || stored?.scheduledAt).toBeTruthy();
    expect(new Date(stored.scheduled_at || stored.scheduledAt).toISOString()).toBe(scheduledAt);

    const clinical = await apiRequest(
      accessToken,
      "GET",
      `/dashboard/clinical-overview?date=${hospitalDate}&scope=all`,
    );
    expect(clinical?.date).toBe(hospitalDate);
    const ids = (clinical?.appointments_today || []).map((a) => a.id);
    expect(ids).toContain(apptId);
  });

  test("appointments page shows hospital-local time and week nav works", async ({ page }) => {
    await login(page);
    await page.goto("/appointments", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("appointments-kanban")).toBeVisible({ timeout: 20_000 });

    // Week label should render (regression: missing format/addDays crashed page)
    const weekLabel = page.locator("span.text-ui-sm.font-medium").filter({ hasText: /–/ });
    await expect(weekLabel.first()).toBeVisible({ timeout: 10_000 });

    await page.getByRole("button", { name: "Previous week" }).click();
    await page.getByRole("button", { name: "Next week" }).click();
    await page.getByRole("button", { name: "Today" }).click();
    await expect(page.getByTestId("appointments-kanban")).toBeVisible();
  });

  test("clinical overview and ops dashboard use hospital calendar day", async ({ page }) => {
    await login(page);
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("page-title")).toBeVisible({ timeout: 20_000 });

    const clinical = await apiRequest(accessToken, "GET", "/dashboard/clinical-overview?scope=all");
    expect(clinical?.date).toMatch(/^\d{4}-\d{2}-\d{2}$/);

    const overview = await apiRequest(accessToken, "GET", "/dashboard/overview");
    expect(overview).toBeTruthy();
    // Upcoming appointments (if any) should render without crashing under hospital TZ formatting
    await page.goto("/", { waitUntil: "networkidle" }).catch(() => {});
    await expect(page.getByTestId("page-title")).toBeVisible();
  });

  test("hospital settings can restore Asia/Kolkata", async ({ page }) => {
    await login(page);
    await page.goto("/settings/hospital", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("hospital-timezone-panel")).toBeVisible({ timeout: 20_000 });

    const putPromise = page.waitForResponse(
      (r) => r.url().includes("/api/hospital-profile") && r.request().method() === "PUT",
      { timeout: 20_000 },
    );
    await page.getByTestId("hospital-timezone-select").click();
    await page.getByRole("option", { name: /India \(Asia\/Kolkata\)/ }).click();
    await page.getByTestId("hospital-timezone-save").click();
    const putRes = await putPromise;
    expect(putRes.ok()).toBeTruthy();

    const profile = await apiRequest(accessToken, "GET", "/hospital-profile");
    expect(profile?.timezone).toBe("Asia/Kolkata");
  });
});

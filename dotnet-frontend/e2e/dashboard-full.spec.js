const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { seedDashboardData } = require("./helpers/seed-dashboard-data");
const { E2E_DASHBOARD_PATIENT_NAME, E2E_TEST_PHONE } = require("./helpers/constants");
const { reportBug, clearBugLog } = require("./helpers/bug-log");

test.describe.configure({ mode: "serial" });

test.describe("Dashboard full coverage", () => {
  /** @type {{ patientId: string, patientName: string }} */
  let seed;

  test.beforeAll(async () => {
    clearBugLog();
    seed = await seedDashboardData();
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
    const overviewPromise = page.waitForResponse(
      (r) => r.url().includes("/api/dashboard/overview") && r.ok(),
      { timeout: 30_000 },
    );
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await overviewPromise;
    await expect(page.getByTestId("page-title")).toBeVisible({ timeout: 20_000 });
  });

  test("header: date/time and notification bell", async ({ page }) => {
    const dateTime = page.getByTestId("dashboard-datetime").locator("visible=true");
    await expect(dateTime).toBeVisible();
    await expect(dateTime).toContainText(/\d/);

    const bell = page.getByTestId("notification-bell-desktop").first();
    await expect(bell).toBeVisible();
    await bell.click();
    await expect(page.getByTestId("notification-dropdown")).toBeVisible();
    await page.keyboard.press("Escape");
  });

  test("stat cards show real values after seeding", async ({ page }) => {
    const appts = page.getByTestId("stat-appointments");
    await expect(appts).toBeVisible();
    await expect(appts).toContainText("Today's Appointments");
    const apptText = await appts.textContent();
    expect(apptText, "Today's Appointments should show a numeric count").toMatch(/[1-9]\d*/);

    const newPatients = page.getByTestId("stat-new-inquiries");
    await expect(newPatients).toBeVisible();
    await expect(newPatients).toContainText("New Patients");
    await expect(newPatients).toContainText(/\d/);

    const revenue = page.getByTestId("stat-revenue");
    await expect(revenue).toBeVisible();
    await expect(revenue).toContainText("Revenue (MTD)");
    await expect(revenue).toContainText(/₹/);

    const careScore = page.getByTestId("stat-care-score");
    await expect(careScore).toBeVisible();
    await expect(careScore).toContainText("Care Score");
    await expect(careScore).toContainText(/\d(\.\d)?\/5/);
  });

  test("appointments overview chart section", async ({ page }) => {
    const section = page.getByTestId("dashboard-appointments-overview");
    await expect(section).toBeVisible();
    await expect(section.getByText("Appointments Overview")).toBeVisible();

    const legend = page.getByTestId("dashboard-appt-chart-legend");
    await expect(legend).toBeVisible();
    await expect(legend).toContainText("Scheduled");
    await expect(legend).toContainText("Completed");

    await expect(page.getByTestId("dashboard-appt-week-toggle")).toContainText("This Week");

    const chart = page.getByTestId("dashboard-appt-chart");
    await expect(chart.locator("svg.recharts-surface").first()).toBeVisible();
  });

  test("upcoming appointments list shows seeded patient", async ({ page }) => {
    const section = page.getByTestId("dashboard-upcoming-appointments");
    await expect(section).toBeVisible();
    await expect(section.getByText("Upcoming Appointments")).toBeVisible();

    const list = page.getByTestId("dashboard-upcoming-list");
    await expect(list).toBeVisible();

    const empty = page.getByTestId("dashboard-upcoming-empty");
    if (await empty.isVisible().catch(() => false)) {
      reportBug({
        severity: "High",
        page: "/",
        title: "Upcoming appointments empty after seeding",
        expected: `At least one row for ${E2E_DASHBOARD_PATIENT_NAME}`,
        actual: "No upcoming appointments",
      });
      test.fail(true, "Upcoming list empty after seed");
    }

    await expect(list).toContainText(E2E_DASHBOARD_PATIENT_NAME, { timeout: 15_000 });
    await expect(page.getByTestId("dashboard-upcoming-row-0")).toBeVisible();
  });

  test("revenue overview MTD and chart", async ({ page }) => {
    const section = page.getByTestId("dashboard-revenue-overview");
    await expect(section).toBeVisible();
    await expect(section.getByText("Revenue Overview")).toBeVisible();
    await expect(section.getByText("Total Revenue (MTD)")).toBeVisible();

    const mtdValue = page.getByTestId("dashboard-revenue-mtd-value");
    await expect(mtdValue).toBeVisible();
    const revenueText = await mtdValue.textContent();
    expect(revenueText).toMatch(/₹/);
    expect(revenueText).not.toMatch(/^₹\s*0$/);

    const chart = page.getByTestId("dashboard-revenue-chart");
    await expect(chart.locator("svg.recharts-surface").first()).toBeVisible();
  });

  test("CareFlow AI insights and View Insight navigation", async ({ page }) => {
    const section = page.getByTestId("dashboard-ai-insights");
    await expect(section).toBeVisible();
    await expect(section.getByText("CareFlow AI Insights")).toBeVisible();
    await expect(page.getByTestId("dashboard-ai-insight-title")).toContainText("no-show");
    await expect(page.getByTestId("dashboard-ai-insight-body")).toBeVisible();

    await page.getByTestId("dashboard-view-insight").click();
    await page.waitForURL(/\/missed-revenue/, { timeout: 15_000 });
    await expect(page).toHaveURL(/\/missed-revenue/);
  });

  test("dashboard search opens command palette and finds seeded patient", async ({ page }) => {
    const searchBtn = page
      .getByTestId("open-command-palette")
      .or(page.getByTestId("open-command-palette-mobile"))
      .first();
    await expect(searchBtn).toBeVisible();
    await searchBtn.click();

    const input = page.getByTestId("cmd-palette-input");
    await expect(input).toBeVisible();

    const searchResponse = page.waitForResponse(
      (r) => r.url().includes("/api/patients?q=") && r.request().method() === "GET" && r.ok(),
      { timeout: 15_000 },
    );
    await input.fill(E2E_DASHBOARD_PATIENT_NAME);
    await searchResponse;

    const dialog = page.getByRole("dialog");
    const patientItem = dialog.locator("[cmdk-item]").filter({ hasText: E2E_DASHBOARD_PATIENT_NAME }).first();
    await expect(patientItem).toBeVisible({ timeout: 15_000 });

    await patientItem.click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]+/, { timeout: 15_000 });
    await expect(page.getByTestId("patient-name")).toContainText(E2E_DASHBOARD_PATIENT_NAME);
  });

  test("stat card links navigate correctly", async ({ page }) => {
    await page.getByTestId("stat-appointments").click();
    await page.waitForURL(/\/appointments/, { timeout: 15_000 });
    await expect(page.getByTestId("appointments-kanban")).toBeVisible();

    await page.goto("/", { waitUntil: "networkidle" });
    await page.getByTestId("stat-new-inquiries").click();
    await page.waitForURL(/\/patients/, { timeout: 15_000 });
    await expect(page.getByTestId("patients-search")).toBeVisible();
  });

  test("sidebar navigation from dashboard", async ({ page }) => {
    await page.getByTestId("nav-appointments").click();
    await page.waitForURL(/\/appointments/, { timeout: 15_000 });
    await expect(page.getByTestId("appointments-kanban")).toBeVisible();

    await page.getByTestId("nav-dashboard").click();
    await page.waitForURL((url) => url.pathname === "/", { timeout: 15_000 });

    await page.getByTestId("nav-analytics").click();
    await page.waitForURL(/\/missed-revenue/, { timeout: 15_000 });
  });
});

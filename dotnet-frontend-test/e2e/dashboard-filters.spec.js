const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { clearBugLog } = require("./helpers/bug-log");

test.describe("Dashboard period filters", () => {
  test.beforeAll(() => clearBugLog());

  test.beforeEach(async ({ page }) => {
    await login(page);
    const overviewPromise = page.waitForResponse(
      (r) => r.url().includes("/api/dashboard/overview") && r.ok(),
      { timeout: 30_000 },
    );
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await overviewPromise;
    await expect(page.getByTestId("dashboard-appointments-overview")).toBeVisible({ timeout: 20_000 });
  });

  test("appointments week dropdown changes chart series", async ({ page }) => {
    const toggle = page.getByTestId("dashboard-appt-week-toggle");
    await expect(toggle).toContainText("This Week");

    const chart = page.getByTestId("dashboard-appt-chart");
    const initialSvg = await chart.locator("svg.recharts-surface").first().innerHTML();

    await toggle.click();
    await page.getByTestId("dashboard-appt-period-last_week").click();
    await expect(toggle).toContainText("Last Week");

    await expect.poll(async () => {
      const html = await chart.locator("svg.recharts-surface").first().innerHTML();
      return html !== initialSvg;
    }, { timeout: 10_000 }).toBe(true);
  });

  test("revenue period filter updates label and value", async ({ page }) => {
    const toggle = page.getByTestId("dashboard-revenue-period-toggle");
    await expect(toggle).toContainText("This Month");
    await expect(page.getByText("Total Revenue (MTD)")).toBeVisible();

    await toggle.click();
    await page.getByRole("menuitem", { name: "This Week", exact: true }).click();
    await expect(toggle).toContainText("This Week");
    await expect(page.getByText("Total Revenue (This Week)")).toBeVisible();

    const mtdValue = page.getByTestId("dashboard-revenue-mtd-value");
    await expect(mtdValue).toContainText(/₹/);
  });
});

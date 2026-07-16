const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");

test.describe.configure({ mode: "serial" });

test.describe("Doctor clinical dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, { email: "doctor@cureflow.in", password: "admin123" });
    await page.goto("/", { waitUntil: "domcontentloaded" });
  });

  test("doctor lands on clinical dashboard, not admin revenue widgets", async ({ page }) => {
    await expect(page.getByTestId("doctor-appointment-queue")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("doctor-stat-today")).toBeVisible();
    await expect(page.locator("text=Total Revenue (MTD)")).toHaveCount(0);
    await expect(page.getByTestId("doctor-scope-mine")).toBeVisible();
  });

  test("default scope shows my appointments today", async ({ page }) => {
    await expect(page.getByTestId("doctor-scope-mine")).toHaveClass(/064E3B|bg-\[#064E3B\]/);
    const rows = page.locator("[data-testid^='doctor-appt-row-']");
    await expect(rows.first()).toBeVisible({ timeout: 20_000 });
    const count = await rows.count();
    expect(count).toBeGreaterThan(0);
  });

  test("all doctors scope shows colleague appointments", async ({ page }) => {
    await page.getByTestId("doctor-scope-all").click();
    await expect(page.getByTestId("doctor-appointment-queue")).toBeVisible();
    const rows = page.locator("[data-testid^='doctor-appt-row-']");
    await expect(rows.first()).toBeVisible({ timeout: 20_000 });
    await expect(rows).not.toHaveCount(0);
  });

  test("open chart from appointment navigates to patient today tab with consultation panel", async ({ page }) => {
    const firstOpen = page.locator("[data-testid^='doctor-open-chart-']").first();
    await expect(firstOpen).toBeVisible({ timeout: 20_000 });
    await firstOpen.click();
    await page.waitForURL(/\/patients\/[^/]+\?tab=today&appointment=/, { timeout: 20_000 });
    await expect(page.getByTestId("consultation-panel")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("tab-today")).toBeVisible();
    await expect(page.getByTestId("today-overview")).toBeVisible();
  });

  test("full consultation workflow: visit, prescription, print, complete", async ({ page }) => {
    const firstOpen = page.locator("[data-testid^='doctor-open-chart-']").first();
    await expect(firstOpen).toBeVisible({ timeout: 20_000 });
    await firstOpen.click();
    await page.waitForURL(/\/patients\/[^/]+\?tab=today&appointment=/, { timeout: 20_000 });
    await expect(page.getByTestId("consultation-panel")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("consultation-visit-started")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("consultation-new-rx").click();
    await expect(page.getByTestId("rx-doctor")).toBeVisible({ timeout: 10_000 });
    await page.getByTestId("rx-drug-0").fill("Paracetamol 500mg");
    await page.getByTestId("rx-reason-0").fill("Fever and body ache");
    await page.getByTestId("rx-save").click();
    await expect(page.locator("[data-testid^='rx-row-']").first()).toBeVisible({ timeout: 10_000 });

    const printBtn = page.locator("[data-testid^='rx-print-']").first();
    await expect(printBtn).toBeVisible({ timeout: 10_000 });

    await page.getByTestId("tab-today").click();
    await page.getByTestId("consultation-complete").click();
    await expect(page.getByText("Consultation completed").first()).toBeVisible({ timeout: 10_000 });
  });

  test("admin still sees operations dashboard", async ({ page }) => {
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("doctor-appointment-queue")).toHaveCount(0);
    await expect(page.getByTestId("open-command-palette")).toBeVisible({ timeout: 20_000 });
  });
});

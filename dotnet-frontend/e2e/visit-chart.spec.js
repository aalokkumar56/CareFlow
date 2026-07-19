const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin, searchPatients } = require("./helpers/api");
const path = require("path");
const fs = require("fs");

test.describe.configure({ mode: "serial" });

const TARGET_PATIENT = "E2E Detail Patient 1784447167014 Long Name";

test.describe("Visit chart", () => {
  let adminToken;
  let patientId;

  test.beforeAll(async () => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    adminToken = admin.accessToken;

    const items = await searchPatients(adminToken, "1784447167014");
    const patient = items.find((p) => (p.name || "").includes("1784447167014"));
    expect(patient, `Patient ${TARGET_PATIENT} should exist`).toBeTruthy();
    patientId = patient.id;
  });

  test("Visit chart is 2nd tab and read-only with full-screen paper viewer", async ({ page }) => {
    await login(page);
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });

    const tabs = page.getByTestId("patient-tabs").locator('[data-testid^="tab-"]');
    await expect(tabs.nth(0)).toHaveAttribute("data-testid", "tab-today");
    await expect(tabs.nth(1)).toHaveAttribute("data-testid", "tab-visit-chart");
    await expect(tabs.last()).toHaveAttribute("data-testid", "tab-details");

    await page.getByTestId("tab-visit-chart").click();
    await expect(page.getByTestId("visit-chart")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId("visit-chart-feed")).toBeVisible();
    await expect(page.getByTestId(/^visit-card-/).first().getByText("1) Vitals")).toBeVisible();
    await expect(page.locator('[data-testid^="add-paper-note-"]')).toHaveCount(0);
    await expect(page.getByTestId("visit-chart")).toContainText("Read-only history");

    const thumb = page.locator('[data-testid^="paper-note-thumb-"]').first();
    await expect(thumb).toBeVisible({ timeout: 10_000 });
    await thumb.click();
    await expect(page.getByTestId("paper-note-viewer")).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId("paper-note-viewer-image")).toBeVisible({ timeout: 10_000 });
    await page.getByTestId("paper-note-viewer-close").click();
    await expect(page.getByTestId("paper-note-viewer")).toHaveCount(0);
  });

  test("Jump to date scrolls to the selected past visit", async ({ page }) => {
    await login(page);
    await page.goto(`/patients/${patientId}?tab=visit-chart`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("visit-chart-feed")).toBeVisible({ timeout: 20_000 });

    const cards = page.locator("[data-visit-date]");
    const count = await cards.count();
    expect(count).toBeGreaterThanOrEqual(2);

    // Prefer a visit that is not the first (newest) card so scroll is observable
    const targetDate = await cards.nth(1).getAttribute("data-visit-date");
    expect(targetDate).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    const [year, month, day] = targetDate.split("-").map(Number);
    const monthName = new Date(year, month - 1, 1).toLocaleString("en-US", { month: "long" });
    const dayNum = String(day);

    await cards.first().scrollIntoViewIfNeeded();

    await page.getByTestId("visit-chart-jump-date").click();
    await expect(page.getByText(/Highlighted dates have visits/i)).toBeVisible();

    // Day buttons have no aria-label in this DayPicker build — match visible day number
    // while staying on the target month (caption e.g. "July 2026").
    let clicked = false;
    for (let i = 0; i < 14 && !clicked; i += 1) {
      const grid = page.getByRole("grid", { name: `${monthName} ${year}` });
      if (await grid.count()) {
        const dayBtn = grid.locator('button[name="day"]:not([disabled])').filter({
          hasText: new RegExp(`^${dayNum}$`),
        });
        if (await dayBtn.count()) {
          await dayBtn.first().click();
          clicked = true;
          break;
        }
      }
      await page.getByRole("button", { name: /previous month/i }).click();
      await page.waitForTimeout(150);
    }
    expect(clicked, `Could not find enabled calendar day for ${targetDate}`).toBe(true);

    const targetCard = page.locator(`[data-visit-date="${targetDate}"]`);
    await expect(targetCard).toBeInViewport({ timeout: 8000 });
    await expect(targetCard).toHaveClass(/border-\[#064E3B\]/);
  });

  test("Patients list row click and double-click open patient detail", async ({ page }) => {
    await login(page);
    await page.goto("/patients", { waitUntil: "networkidle" });
    await page.getByTestId("patients-search").fill("1784447167014");
    await page.waitForTimeout(400);
    const row = page.locator(`[data-testid="patient-row-${patientId}"]`).first();
    await expect(row).toBeVisible({ timeout: 15_000 });

    await row.dblclick();
    await expect(page).toHaveURL(new RegExp(`/patients/${patientId}`), { timeout: 15_000 });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });

    await page.goto("/patients", { waitUntil: "networkidle" });
    await page.getByTestId("patients-search").fill("1784447167014");
    await page.waitForTimeout(400);
    const rowAgain = page.locator(`[data-testid="patient-row-${patientId}"]`).first();
    await expect(rowAgain).toBeVisible({ timeout: 15_000 });
    await rowAgain.click();
    await expect(page).toHaveURL(new RegExp(`/patients/${patientId}`), { timeout: 15_000 });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
  });

  test("uploads paper note from Prescriptions and it appears on Visit chart", async ({ page }) => {
    await login(page);
    await page.goto(`/patients/${patientId}?tab=prescriptions`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("ehr-prescriptions")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("rx-paper-note-upload")).toBeVisible();

    const tmpPng = path.join(__dirname, "test-results", `rx-paper-${Date.now()}.png`);
    fs.mkdirSync(path.dirname(tmpPng), { recursive: true });
    // 1x1 PNG
    fs.writeFileSync(
      tmpPng,
      Buffer.from([
        0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xde, 0x00, 0x00, 0x00, 0x0c, 0x49, 0x44, 0x41, 0x54, 0x08, 0xd7, 0x63, 0xf8, 0xcf, 0xc0, 0x00,
        0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xdd, 0x8d, 0xb4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e,
        0x44, 0xae, 0x42, 0x60, 0x82,
      ]),
    );

    await page.getByTestId("rx-paper-note-title").fill("Rx desk scan");
    await page.getByTestId("rx-paper-note-input").setInputFiles(tmpPng);
    await expect(page.getByText("Paper note added").first()).toBeVisible({ timeout: 15_000 });

    await page.getByTestId("tab-visit-chart").click();
    await expect(page.getByTestId("visit-chart-feed")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText("Rx desk scan").first()).toBeVisible({ timeout: 10_000 });
  });
});

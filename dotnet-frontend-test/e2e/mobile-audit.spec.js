const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin, createPatient } = require("./helpers/api");

const MOBILE_VIEWPORT = { width: 375, height: 812 };

/** @param {import('@playwright/test').Page} page */
async function assertNoHorizontalOverflow(page, label) {
  const overflow = await page.evaluate(() => {
    const doc = document.documentElement;
    return doc.scrollWidth > doc.clientWidth + 2;
  });
  expect(overflow, `${label}: page should not scroll horizontally`).toBe(false);
}

/** @param {import('@playwright/test').Page} page */
async function assertButtonReachable(page, locator, viewportHeight = MOBILE_VIEWPORT.height) {
  await locator.scrollIntoViewIfNeeded();
  const box = await locator.boundingBox();
  expect(box, "Button should have a bounding box").not.toBeNull();
  expect(box.y + box.height, "Button should not be clipped below viewport").toBeLessThanOrEqual(viewportHeight + 4);
  expect(box.y, "Button should not be clipped above viewport").toBeGreaterThanOrEqual(-2);
}

/** @param {import('@playwright/test').Page} page */
async function assertMobileNavToggle(page) {
  const toggle = page.getByTestId("mobile-nav-toggle").first();
  await expect(toggle).toBeVisible();
  await toggle.click();
  const mobileSheet = page.locator('[role="dialog"]');
  await expect(mobileSheet.getByTestId("nav-patients")).toBeVisible({ timeout: 10_000 });
  await page.keyboard.press("Escape");
}

test.describe("Mobile viewport audit (375×812)", () => {
  test.use({ viewport: MOBILE_VIEWPORT });

  test("login page — form reachable, no overflow", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByTestId("login-form")).toBeVisible();
    await assertNoHorizontalOverflow(page, "Login");

    const submit = page.getByTestId("login-submit");
    await assertButtonReachable(page, submit);
    await expect(page.getByTestId("login-email")).toBeVisible();
    await expect(page.getByTestId("login-password")).toBeVisible();
  });

  test("dashboard — nav toggle, no overflow", async ({ page }) => {
    await login(page);
    await page.goto("/");
    await page.waitForLoadState("domcontentloaded");
    await assertNoHorizontalOverflow(page, "Dashboard");
    await assertMobileNavToggle(page);
  });

  test("patients list — search and new patient reachable", async ({ page }) => {
    await login(page);
    await page.goto("/patients");
    await expect(page.getByTestId("patients-search")).toBeVisible();
    await assertNoHorizontalOverflow(page, "Patients list");
    await assertMobileNavToggle(page);

    const newBtn = page.getByTestId("new-patient-btn");
    await assertButtonReachable(page, newBtn);
    await expect(page.getByTestId("app-sidebar")).toBeHidden();
  });

  test("patient detail — name visible, no overflow", async ({ page }) => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const stamp = Date.now();
    const name = `E2E Mobile Patient ${stamp}`;
    const patient = await createPatient(admin.accessToken, name, `91${String(stamp).slice(-8)}`);
    const patientId = patient.id || patient.patient?.id;

    await login(page);
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toHaveText(name);
    await assertNoHorizontalOverflow(page, "Patient detail");
    await assertMobileNavToggle(page);
  });

  test("appointments — kanban and new button reachable", async ({ page }) => {
    await login(page);
    await page.goto("/appointments");
    await expect(page.getByTestId("appointments-kanban")).toBeVisible();
    await assertNoHorizontalOverflow(page, "Appointments");
    await assertMobileNavToggle(page);
    await assertButtonReachable(page, page.getByTestId("new-appt-btn"));
  });

  test("analytics — stat cards and nav on mobile", async ({ page }) => {
    await login(page);
    await page.goto("/missed-revenue", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("analytics-total-loss")).toBeVisible({ timeout: 20_000 });
    await assertNoHorizontalOverflow(page, "Analytics");
    await assertMobileNavToggle(page);
    await expect(page.getByTestId("analytics-item-count")).toBeVisible();
  });

  test("settings notifications — scrollable prefs, save reachable", async ({ page }) => {
    await login(page);
    await page.goto("/settings/notifications");
    await expect(page.getByTestId("page-title")).toHaveText("Notifications");
    await assertNoHorizontalOverflow(page, "Settings notifications");
    await assertMobileNavToggle(page);
    await expect(page.getByTestId("app-sidebar")).toBeHidden();

    const scrollContainer = page.getByTestId("notifications-prefs-scroll");
    await expect(scrollContainer).toBeVisible();

    const { scrollHeight, clientHeight } = await scrollContainer.evaluate((el) => ({
      scrollHeight: el.scrollHeight,
      clientHeight: el.clientHeight,
    }));
    expect(scrollHeight).toBeGreaterThan(clientHeight);

    await scrollContainer.evaluate((el) => {
      el.scrollTop = el.scrollHeight;
    });

    const saveBtn = page.getByRole("button", { name: "Save preferences" });
    await assertButtonReachable(page, saveBtn, MOBILE_VIEWPORT.height);
    await expect(saveBtn).toBeEnabled();
  });
});

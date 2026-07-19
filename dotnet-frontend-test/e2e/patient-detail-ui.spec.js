const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin, createPatient } = require("./helpers/api");
const { ensureTestUsers, E2E_PASSWORD } = require("./helpers/test-users");
const { PATIENT_TABS, CLINICAL_TABS } = require("./helpers/routes");
const { collectConsoleErrors } = require("./helpers/ui-audit");

test.describe.configure({ mode: "serial" });

test.describe("Patient detail UI", () => {
  let adminToken;
  let patientId;
  let patientName;
  let testUsers;

  test.beforeAll(async () => {
    const stamp = Date.now();
    testUsers = await ensureTestUsers(stamp);
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    adminToken = admin.accessToken;

    patientName = `E2E Detail Patient ${stamp} Long Name`;
    const patient = await createPatient(
      adminToken,
      patientName,
      `91${String(stamp).slice(-8)}`,
    );
    patientId = patient.id || patient.patient?.id;
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
  });

  test("shows single canonical patient name in content, not duplicated in breadcrumb", async ({ page }) => {
    const pageTitle = page.getByTestId("page-title");
    await expect(pageTitle).toHaveCount(0);

    const canonicalName = page.getByTestId("patient-name");
    await expect(canonicalName).toBeVisible();
    await expect(canonicalName).toHaveText(patientName);

    const breadcrumb = page.getByTestId("page-breadcrumb");
    await expect(breadcrumb).toBeVisible();
    await expect(breadcrumb.getByRole("link", { name: "Patients" })).toBeVisible();
    await expect(breadcrumb.getByText("Profile")).toBeVisible();
    // Full patient name must appear once (header), not again in the trail
    await expect(breadcrumb).not.toContainText(patientName);
  });

  test("full patient name is visible and not truncated to id-like prefix", async ({ page }) => {
    const nameEl = page.getByTestId("patient-name");
    const text = await nameEl.textContent();
    expect(text).toBe(patientName);
    expect(text).not.toMatch(/^\d{6}\.\.\.$/);
    expect(text.length).toBeGreaterThan(10);

    const truncated = await nameEl.evaluate((el) => {
      const style = window.getComputedStyle(el);
      return el.scrollWidth > el.clientWidth + 2 || style.textOverflow === "ellipsis";
    });
    expect(truncated, "Primary patient name should not be CSS-truncated").toBe(false);
  });

  test("breadcrumb and back navigation return to patients list", async ({ page }) => {
    await page.getByTestId("page-breadcrumb").getByRole("link", { name: "Patients" }).click();
    await page.waitForURL(/\/patients\/?$/, { timeout: 15_000 });
    await expect(page.getByTestId("patients-search")).toBeVisible();

    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    await page.getByTestId("patient-back-link").click();
    await page.waitForURL(/\/patients\/?$/, { timeout: 15_000 });
    await expect(page.getByTestId("patients-search")).toBeVisible();
  });

  test("all tabs are clickable without errors", async ({ page }) => {
    const consoleErrors = await collectConsoleErrors(page);

    for (const tabId of PATIENT_TABS) {
      const tab = page.getByTestId(tabId);
      if ((await tab.count()) === 0) {
        if (CLINICAL_TABS.includes(tabId)) continue;
        throw new Error(`Expected tab ${tabId} to exist for admin`);
      }

      await tab.click();
      await page.waitForTimeout(400);
      await expect(tab).toHaveAttribute("data-state", "active");

      const panel = page.locator('[role="tabpanel"][data-state="active"]');
      await expect(panel).toBeVisible();
    }

    const ignored = consoleErrors.filter(
      (e) => !e.includes("favicon") && !e.includes("404") && !e.includes("net::ERR"),
    );
    expect(ignored, `Console errors: ${ignored.join("; ")}`).toHaveLength(0);
  });

  test("no horizontal overflow on patient detail", async ({ page }) => {
    const overflow = await page.evaluate(() => {
      const doc = document.documentElement;
      return doc.scrollWidth > doc.clientWidth + 2;
    });
    expect(overflow, "Page should not scroll horizontally").toBe(false);
  });

  test("page loads without console errors", async ({ page }) => {
    const errors = [];
    page.on("console", (msg) => {
      if (msg.type() === "error") errors.push(msg.text());
    });
    page.on("pageerror", (err) => errors.push(err.message));

    await page.reload({ waitUntil: "networkidle" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });

    const ignored = errors.filter(
      (e) => !e.includes("favicon") && !e.includes("404") && !e.includes("net::ERR"),
    );
    expect(ignored, `Console errors: ${ignored.join("; ")}`).toHaveLength(0);
  });

  test("nurse: WhatsApp icon hidden", async ({ page }) => {
    const creds = testUsers.nurse;
    await login(page, { email: creds.email, password: creds.password });
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("patient-whatsapp-icon")).toHaveCount(0);
  });

  test("reception: WhatsApp icon visible", async ({ page }) => {
    const creds = testUsers.reception;
    await login(page, { email: creds.email, password: creds.password });
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("patient-whatsapp-icon")).toBeVisible();
  });
});

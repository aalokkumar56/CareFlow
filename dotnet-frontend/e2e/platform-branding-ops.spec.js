const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { loginAsPlatformOps } = require("./helpers/platform-auth");
const { registerTenant } = require("./helpers/api");
const { HOSPITAL_A } = require("./helpers/multi-hospital");

test.describe("Platform branding and ops UI", () => {
  test("login and signup show CureFlow brand, not cure&care", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByTestId("cureflow-brand")).toBeVisible();
    await expect(page.locator("text=cure&care")).toHaveCount(0);

    await page.goto("/signup");
    await expect(page.getByTestId("cureflow-brand")).toBeVisible();
    await expect(page.locator("text=cure&care")).toHaveCount(0);
  });

  test("platform login shows CureFlow Platform", async ({ page }) => {
    await page.goto("/platform/login");
    await expect(page.getByTestId("cureflow-brand")).toBeVisible();
    await expect(page.getByTestId("platform-login-title")).toContainText("CureFlow Platform");
  });

  test("demo hospital sidebar shows tenant name Cure & Care Hospital", async ({ page }) => {
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto("/");
    await expect(page.getByTestId("sidebar-hospital-name")).toContainText("Cure & Care Hospital");
    await expect(page.getByTestId("sidebar-hospital-name")).not.toContainText("cure&care");
  });

  test("multi-hospital sidebar shows correct tenant name", async ({ page }) => {
    await login(page, {
      email: HOSPITAL_A.adminEmail,
      password: HOSPITAL_A.password,
    });
    await page.goto("/");
    await expect(page.getByTestId("sidebar-hospital-name")).toContainText(HOSPITAL_A.name);
  });

  test("full ops UI approve flow via platform console", async ({ page }) => {
    const runId = Date.now();
    const adminEmail = `ops-ui-${runId}@e2e.cureflow.test`;
    const hospitalName = `Ops UI Clinic ${runId}`;

    const tenant = await registerTenant({
      hospitalName,
      adminName: "Ops UI Admin",
      adminEmail,
      adminPassword: "Test@12345!",
    });
    expect(tenant.lifecycle_status).toBe("pending_approval");

    await loginAsPlatformOps(page);
    await expect(page.getByTestId("platform-tenants-title")).toBeVisible();

    const slug = tenant.slug;
    const row = page.getByTestId(`platform-tenant-${slug}`);
    await expect(row).toBeVisible({ timeout: 20_000 });
    await expect(row).toContainText("pending approval");

    await row.getByTestId(`platform-approve-${slug}`).click();
    await expect(page.getByTestId("platform-approve-dialog")).toBeVisible();
    await page.getByTestId("platform-approve-confirm").click();

    await page.getByTestId("platform-tab-active").click();
    await expect(page.getByTestId(`platform-tenant-${slug}`)).toBeVisible({ timeout: 15_000 });

    await page.goto("/login");
    await page.getByTestId("login-email").fill(adminEmail);
    await page.getByTestId("login-password").fill("Test@12345!");
    await page.getByTestId("login-submit").click();
    await page.waitForURL(/\/onboarding/, { timeout: 20_000 });
  });
});

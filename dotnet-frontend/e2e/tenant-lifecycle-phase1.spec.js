const { test, expect } = require("@playwright/test");
const { apiLogin, registerTenant, platformApproveTenant, apiRequest } = require("./helpers/api");

const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";
const runId = Date.now();

test.describe.configure({ mode: "serial" });

test.describe("Tenant lifecycle — signup → pending → approve → onboarding → dashboard", () => {
  const hospitalName = `E2E Lifecycle Hospital ${runId}`;
  const adminEmail = `lifecycle-${runId}@e2e.cureflow.test`;
  const adminPassword = "Test@12345!";
  let tenantId;

  test("register tenant starts in pending_approval", async () => {
    const tenant = await registerTenant({
      hospitalName,
      adminName: "Lifecycle Admin",
      adminEmail,
      adminPassword,
      phone: "919900000099",
    });
    tenantId = tenant.id;
    expect(tenant.lifecycle_status).toBe("pending_approval");
  });

  test("pending admin login lands on waiting room only", async ({ page }) => {
    await page.goto("/login", { waitUntil: "domcontentloaded" });
    await page.getByTestId("login-email").fill(adminEmail);
    await page.getByTestId("login-password").fill(adminPassword);

    const loginResponse = page.waitForResponse(
      (r) => r.url().includes("/api/auth/login") && r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await page.getByTestId("login-submit").click();
    expect((await loginResponse).ok()).toBeTruthy();

    await page.waitForURL(/\/pending-approval/, { timeout: 20_000 });
    await expect(page.getByTestId("pending-approval-title")).toBeVisible();

    await page.goto("/patients", { waitUntil: "domcontentloaded" });
    await page.waitForURL(/\/pending-approval/, { timeout: 20_000 });
  });

  test("platform ops approves tenant", async () => {
    expect(tenantId).toBeTruthy();
    const result = await platformApproveTenant(tenantId);
    expect(result.lifecycle_status?.toLowerCase()).toBe("active");
  });

  test("approved admin completes onboarding and reaches dashboard", async ({ page }) => {
    const session = await apiLogin(adminEmail, adminPassword);
    expect(session.tenant.lifecycle_status).toBe("active");

    await page.goto("/login", { waitUntil: "domcontentloaded" });
    await page.evaluate(({ tokenValue, userValue, tenantValue }) => {
      localStorage.setItem("cureflow_token", tokenValue);
      localStorage.setItem("cureflow_user", JSON.stringify(userValue));
      localStorage.setItem("cureflow_tenant", JSON.stringify(tenantValue));
    }, {
      tokenValue: session.accessToken,
      userValue: session.user,
      tenantValue: session.tenant,
    });
    await page.reload({ waitUntil: "domcontentloaded" });

    await page.goto("/onboarding", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("onboarding-title")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("onboarding-finish").click();

    await page.waitForURL((url) => url.pathname === "/", { timeout: 20_000 });

    const patientsBlocked = await fetch(`${apiURL}/api/patients?page=1&page_size=1`, {
      headers: { Authorization: `Bearer ${session.accessToken}` },
    });
    expect(patientsBlocked.ok).toBeTruthy();
  });

  test("onboarding API marks tenant complete", async () => {
    const session = await apiLogin(adminEmail, adminPassword);
    const refreshed = await apiRequest(session.accessToken, "GET", "/auth/session");
    expect(refreshed.tenant.onboarding_complete).toBe(true);
    expect(refreshed.tenant.lifecycle_status).toBe("active");
  });
});

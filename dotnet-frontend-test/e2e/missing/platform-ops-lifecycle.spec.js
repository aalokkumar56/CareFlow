const { test, expect } = require("@playwright/test");
const { loginAsPlatformOps } = require("../helpers/platform-auth");
const { registerTenant, apiLogin, apiURL } = require("../helpers/api");

test.describe.configure({ mode: "serial" });

const PLATFORM_OPS = {
  email: process.env.PLATFORM_OPS_EMAIL || "ops@cureflow.in",
  password: process.env.PLATFORM_OPS_PASSWORD || "OpsAdmin123!",
};

/** Platform JWT for API lifecycle actions (approve) when UI is on another page. */
async function platformApiLogin() {
  const res = await fetch(`${apiURL}/api/platform/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      email: PLATFORM_OPS.email,
      password: PLATFORM_OPS.password,
    }),
  });
  if (!res.ok) {
    throw new Error(`platform login failed: ${res.status} ${await res.text()}`);
  }
  const data = await res.json();
  return data.access_token || data.accessToken;
}

async function approveTenantViaApi(tenantId) {
  const token = await platformApiLogin();
  const res = await fetch(`${apiURL}/api/platform/tenants/${tenantId}/approve`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
  });
  if (!res.ok) {
    throw new Error(`approve failed: ${res.status} ${await res.text()}`);
  }
  return res.json();
}

async function loginPendingAdmin(page, email, password) {
  await page.goto("/login", { waitUntil: "domcontentloaded" });
  await page.getByTestId("login-email").fill(email);
  await page.getByTestId("login-password").fill(password);

  const loginResponse = page.waitForResponse(
    (r) => r.url().includes("/api/auth/login") && r.request().method() === "POST",
    { timeout: 20_000 },
  );
  await page.getByTestId("login-submit").click();
  expect((await loginResponse).ok()).toBeTruthy();
  await page.waitForURL(/\/pending-approval/, { timeout: 20_000 });
  await expect(page.getByTestId("pending-approval-title")).toBeVisible();
}

test.describe("Platform ops lifecycle — reject, suspend, activate, check status", () => {
  test("reject pending registration via platform console", async ({ page }) => {
    const runId = Date.now();
    const adminEmail = `ops-reject-${runId}@e2e.cureflow.test`;
    const hospitalName = `Ops Reject Clinic ${runId}`;

    const tenant = await registerTenant({
      hospitalName,
      adminName: "Reject Admin",
      adminEmail,
      adminPassword: "Test@12345!",
    });
    expect(tenant.lifecycle_status).toBe("pending_approval");
    const slug = tenant.slug;

    await loginAsPlatformOps(page, PLATFORM_OPS);
    await expect(page.getByTestId("platform-tenants-title")).toBeVisible();

    const row = page.getByTestId(`platform-tenant-${slug}`);
    await expect(row).toBeVisible({ timeout: 20_000 });
    await expect(row).toContainText("pending approval");

    await row.getByTestId(`platform-reject-${slug}`).click();
    await expect(page.getByTestId("platform-reject-dialog")).toBeVisible();
    await page.getByTestId("platform-reject-reason").fill("E2E rejection reason");
    await page.getByTestId("platform-reject-confirm").click();

    await page.getByTestId("platform-tab-rejected").click();
    await expect(page.getByTestId(`platform-tenant-${slug}`)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId(`platform-tenant-${slug}`)).toContainText("rejected");
  });

  test("suspend active tenant then reactivate", async ({ page }) => {
    const runId = Date.now();
    const adminEmail = `ops-suspend-${runId}@e2e.cureflow.test`;
    const hospitalName = `Ops Suspend Clinic ${runId}`;

    const tenant = await registerTenant({
      hospitalName,
      adminName: "Suspend Admin",
      adminEmail,
      adminPassword: "Test@12345!",
    });
    const slug = tenant.slug;
    const approved = await approveTenantViaApi(tenant.id);
    expect(approved.lifecycle_status?.toLowerCase()).toBe("active");

    await loginAsPlatformOps(page, PLATFORM_OPS);
    await page.getByTestId("platform-tab-active").click();

    const row = page.getByTestId(`platform-tenant-${slug}`);
    await expect(row).toBeVisible({ timeout: 20_000 });

    const suspendResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/platform/tenants/${tenant.id}/suspend`) &&
        r.request().method() === "PATCH",
      { timeout: 20_000 },
    );
    await row.getByTestId(`platform-suspend-${slug}`).click();
    expect((await suspendResponse).ok()).toBeTruthy();

    await page.getByTestId("platform-tab-suspended").click();
    const suspendedRow = page.getByTestId(`platform-tenant-${slug}`);
    await expect(suspendedRow).toBeVisible({ timeout: 15_000 });
    await expect(suspendedRow).toContainText("suspended");

    const activateResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/platform/tenants/${tenant.id}/activate`) &&
        r.request().method() === "PATCH",
      { timeout: 20_000 },
    );
    await suspendedRow.getByTestId(`platform-activate-${slug}`).click();
    expect((await activateResponse).ok()).toBeTruthy();

    await page.getByTestId("platform-tab-active").click();
    await expect(page.getByTestId(`platform-tenant-${slug}`)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId(`platform-tenant-${slug}`)).toContainText("active");
  });

  test("pending-approval check status stays then advances after approve", async ({ page }) => {
    const runId = Date.now();
    const adminEmail = `ops-check-${runId}@e2e.cureflow.test`;
    const adminPassword = "Test@12345!";
    const hospitalName = `Ops Check Clinic ${runId}`;

    const tenant = await registerTenant({
      hospitalName,
      adminName: "Check Admin",
      adminEmail,
      adminPassword,
    });
    expect(tenant.lifecycle_status).toBe("pending_approval");

    await loginPendingAdmin(page, adminEmail, adminPassword);
    await expect(page.getByTestId("pending-approval-check")).toBeVisible();

    const staySession = page.waitForResponse(
      (r) => r.url().includes("/api/auth/session") && r.request().method() === "GET",
      { timeout: 20_000 },
    ).catch(() => null);

    await page.getByTestId("pending-approval-check").click();
    await staySession;
    await expect(page).toHaveURL(/\/pending-approval/, { timeout: 10_000 });
    await expect(page.getByTestId("pending-approval-title")).toBeVisible();

    const approved = await approveTenantViaApi(tenant.id);
    expect(approved.lifecycle_status?.toLowerCase()).toBe("active");

    const session = await apiLogin(adminEmail, adminPassword);
    expect(session.tenant.lifecycle_status?.toLowerCase()).toBe("active");

    await page.getByTestId("pending-approval-check").click();
    await page.waitForURL(
      (url) =>
        url.pathname === "/onboarding" ||
        url.pathname === "/" ||
        !url.pathname.includes("pending-approval"),
      { timeout: 20_000 },
    );
    expect(page.url()).not.toMatch(/\/pending-approval/);
  });
});

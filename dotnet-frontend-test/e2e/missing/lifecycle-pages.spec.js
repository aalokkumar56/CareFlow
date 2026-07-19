const { test, expect } = require("@playwright/test");
const { registerTenant, apiLogin } = require("../helpers/api");

test.describe("Lifecycle pages (UI-HIGH-051…054)", () => {
  const stamp = Date.now();

  test("UI-HIGH-051: registration-received page renders and links to login", async ({ page }) => {
    // Signup currently auto-logs into pending-approval; this page remains a public thank-you route.
    await page.goto("/registration-received", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("registration-received-page")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId("registration-received-title")).toHaveText(/Registration received/i);

    await page.getByTestId("registration-received-login").click();
    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });
  });

  test("UI-HIGH-054: signup required fields empty submission blocked", async ({ page }) => {
    await page.goto("/signup", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("signup-form")).toBeVisible();

    await page.getByTestId("signup-submit").click();
    await expect(page).toHaveURL(/\/signup/);

    const hospitalValid = await page.getByTestId("signup-hospital-name").evaluate((el) => el.checkValidity());
    expect(hospitalValid).toBeFalsy();
  });

  test("UI-HIGH-052: signup validation — weak password blocked", async ({ page }) => {
    await page.goto("/signup", { waitUntil: "domcontentloaded" });
    await page.getByTestId("signup-hospital-name").fill(`Weak PW Hospital ${stamp}`);
    await page.getByTestId("signup-admin-name").fill("Weak Admin");
    await page.getByTestId("signup-email").fill(`weak-pw-${stamp}@e2e.cureflow.test`);
    await page.getByTestId("signup-password").fill("short"); // minLength=8

    await page.getByTestId("signup-submit").click();
    await expect(page).toHaveURL(/\/signup/);

    const pwValid = await page.getByTestId("signup-password").evaluate((el) => el.checkValidity());
    expect(pwValid).toBeFalsy();
  });

  test("UI-HIGH-052: signup validation — duplicate email shows error", async ({ page }) => {
    const email = `dup-${stamp}@e2e.cureflow.test`;
    const password = "Test@12345!";

    await registerTenant({
      hospitalName: `Dup Hospital A ${stamp}`,
      adminName: "Dup Admin A",
      adminEmail: email,
      adminPassword: password,
      phone: "919900001111",
    });

    await page.goto("/signup", { waitUntil: "domcontentloaded" });
    await page.getByTestId("signup-hospital-name").fill(`Dup Hospital B ${stamp}`);
    await page.getByTestId("signup-admin-name").fill("Dup Admin B");
    await page.getByTestId("signup-email").fill(email);
    await page.getByTestId("signup-password").fill(password);

    const registerResponse = page.waitForResponse(
      (r) => r.url().includes("/api/auth/register-tenant") && r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await page.getByTestId("signup-submit").click();
    const res = await registerResponse;
    expect(res.ok()).toBeFalsy();

    await expect(page).toHaveURL(/\/signup/);
    await expect(
      page.getByText(/already|exists|duplicate|registered|failed|in use/i).first(),
    ).toBeVisible({ timeout: 15_000 });
  });

  test("UI-HIGH-053: pending-approval logout clears session", async ({ page }) => {
    const email = `pending-logout-${stamp}@e2e.cureflow.test`;
    const password = "Test@12345!";

    await registerTenant({
      hospitalName: `Pending Logout Hospital ${stamp}`,
      adminName: "Pending Logout Admin",
      adminEmail: email,
      adminPassword: password,
      phone: "919900002222",
    });

    const session = await apiLogin(email, password);
    expect(session.tenant.lifecycle_status).toBe("pending_approval");

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
    await page.goto("/pending-approval", { waitUntil: "domcontentloaded" });

    await expect(page.getByTestId("pending-approval-title")).toBeVisible({ timeout: 20_000 });
    await page.getByTestId("pending-approval-logout").click();

    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });
    const cleared = await page.evaluate(() => ({
      token: localStorage.getItem("cureflow_token"),
      user: localStorage.getItem("cureflow_user"),
      tenant: localStorage.getItem("cureflow_tenant"),
    }));
    expect(cleared.token).toBeNull();
    expect(cleared.user).toBeNull();
    expect(cleared.tenant).toBeNull();

    await page.goto("/patients", { waitUntil: "domcontentloaded" });
    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });
  });
});

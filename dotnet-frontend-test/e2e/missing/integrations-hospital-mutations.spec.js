const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, apiRequest } = require("../helpers/api");
const { ensureTestUsers } = require("../helpers/test-users");
const { getPermissionsForUser, PERMISSIONS } = require("../../../dotnet-frontend/src/lib/permissions");

/**
 * Integrations save flows + hospital department mutations.
 */
test.describe.configure({ mode: "serial" });

test.describe("Integrations and hospital mutations", () => {
  const stamp = Date.now();
  const deptName = `E2E Dept ${stamp}`;
  const waBusinessName = `E2E WA Biz ${stamp}`;

  let accessToken;
  let originalWa;
  let originalSms;
  let originalEmail;
  let originalProfile;

  /** Fill the input that is a sibling of the given label inside the open dialog. */
  async function fillDialogField(dialog, labelText, value) {
    const input = dialog.locator("label", { hasText: new RegExp(`^${labelText}$`, "i") }).locator("..").locator("input");
    await expect(input).toBeVisible({ timeout: 10_000 });
    await input.fill(String(value));
  }

  test.beforeAll(async () => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    accessToken = admin.accessToken;
    originalWa = await apiRequest(accessToken, "GET", "/settings/whatsapp");
    originalSms = await apiRequest(accessToken, "GET", "/settings/sms");
    originalEmail = await apiRequest(accessToken, "GET", "/settings/email");
    originalProfile = await apiRequest(accessToken, "GET", "/hospital-profile");
  });

  test.afterAll(async () => {
    if (!accessToken) return;
    try {
      if (originalWa) {
        await apiRequest(accessToken, "POST", "/settings/whatsapp", {
          provider: originalWa.provider || "MetaCloud",
          phoneNumberId: originalWa.phone_number_id || null,
          wabaId: originalWa.waba_id || null,
          verifyToken: originalWa.verify_token || null,
          businessName: originalWa.business_name || "Cure & Care Hospital",
          whatsBizBaseUrl: originalWa.whats_biz_base_url || null,
          enabled: !!originalWa.enabled,
        });
      }
      if (originalSms) {
        await apiRequest(accessToken, "POST", "/settings/sms", {
          gatewayUrl: originalSms.gateway_url || null,
          senderId: originalSms.sender_id || null,
          enabled: !!originalSms.enabled,
        });
      }
      if (originalEmail) {
        await apiRequest(accessToken, "POST", "/settings/email", {
          smtp_host: originalEmail.smtp_host || null,
          smtp_port: originalEmail.smtp_port || 587,
          smtp_username: originalEmail.smtp_username || null,
          from_email: originalEmail.from_email || null,
          from_name: originalEmail.from_name || null,
          use_ssl: originalEmail.use_ssl !== false,
          enabled: !!originalEmail.enabled,
          send_with_whatsapp: originalEmail.send_with_whatsapp !== false,
        });
      }
      if (originalProfile) {
        await apiRequest(accessToken, "PUT", "/hospital-profile", originalProfile);
      }
    } catch {
      /* best-effort restore */
    }
  });

  test("admin saves WhatsApp/SMS/Email and hospital department", async ({ page }) => {
    await login(page);
    await page.goto("/settings/integrations", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("integrations-panel")).toBeVisible({ timeout: 20_000 });

    // WhatsApp
    await page
      .getByTestId("integrations-panel")
      .getByRole("button", { name: /configure/i })
      .first()
      .click();
    let dialog = page.getByRole("dialog");
    await expect(dialog.getByText("WhatsApp Business")).toBeVisible({ timeout: 10_000 });
    await fillDialogField(dialog, "Business Name", waBusinessName);
    const waSave = page.waitForResponse(
      (r) => r.url().includes("/api/settings/whatsapp") && r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await dialog.getByRole("button", { name: /^Save$/i }).click();
    expect((await waSave).ok()).toBeTruthy();
    await expect(page.getByText(/WhatsApp settings saved/i).first()).toBeVisible({ timeout: 10_000 });
    expect((await apiRequest(accessToken, "GET", "/settings/whatsapp")).business_name).toBe(
      waBusinessName,
    );

    // SMS (Configure buttons: 0=WhatsApp, 1=SMS, 2=Email)
    await page
      .getByTestId("integrations-panel")
      .getByRole("button", { name: /configure/i })
      .nth(1)
      .click();
    dialog = page.getByRole("dialog");
    await expect(dialog.getByText("SMS Gateway")).toBeVisible({ timeout: 10_000 });
    await fillDialogField(dialog, "Gateway URL", `https://sms.e2e.test/${stamp}`);
    await fillDialogField(dialog, "API Key", `e2e-sms-key-${stamp}`);
    await fillDialogField(dialog, "Sender ID", "CUREFLOW");
    const smsSwitch = dialog
      .locator("label")
      .filter({ hasText: /Active — enable SMS/i })
      .getByRole("switch");
    if ((await smsSwitch.getAttribute("data-state")) !== "checked") {
      await smsSwitch.click();
    }
    const smsSave = page.waitForResponse(
      (r) => r.url().includes("/api/settings/sms") && r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await dialog.getByRole("button", { name: /^Save$/i }).click();
    expect((await smsSave).ok()).toBeTruthy();
    await expect(page.getByText(/SMS settings saved/i).first()).toBeVisible({ timeout: 10_000 });
    const sms = await apiRequest(accessToken, "GET", "/settings/sms");
    expect(sms.gateway_url).toContain(`sms.e2e.test/${stamp}`);
    expect(sms.enabled).toBe(true);

    // Email
    await page
      .getByTestId("integrations-panel")
      .getByRole("button", { name: /configure/i })
      .nth(2)
      .click();
    dialog = page.getByRole("dialog");
    await expect(dialog.getByText("Email (SMTP)")).toBeVisible({ timeout: 10_000 });
    await fillDialogField(dialog, "SMTP Host", "smtp.e2e.test");
    await fillDialogField(dialog, "From Email", `e2e.from.${stamp}@cureflow.test`);
    const emailSwitch = dialog
      .locator("label")
      .filter({ hasText: /Active — enable email sending/i })
      .getByRole("switch");
    if ((await emailSwitch.getAttribute("data-state")) === "checked") {
      await emailSwitch.click();
    }
    const emailSave = page.waitForResponse(
      (r) => r.url().includes("/api/settings/email") && r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await dialog.getByRole("button", { name: /^Save$/i }).click();
    expect((await emailSave).ok()).toBeTruthy();
    await expect(page.getByText(/Email settings saved/i).first()).toBeVisible({ timeout: 10_000 });
    const email = await apiRequest(accessToken, "GET", "/settings/email");
    expect(email.smtp_host).toBe("smtp.e2e.test");
    expect(email.from_email).toBe(`e2e.from.${stamp}@cureflow.test`);
    expect(email.enabled).toBe(false);

    // Hospital departments
    await page.goto("/settings/hospital", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("departments-heading")).toBeVisible({ timeout: 20_000 });
    await page.getByPlaceholder("e.g. Cardiology").fill(deptName);
    await page.getByRole("button", { name: /^Add$/i }).click();
    await expect(page.getByText(deptName, { exact: true })).toBeVisible();
    const deptSave = page.waitForResponse(
      (r) => r.url().includes("/api/hospital-profile") && r.request().method() === "PUT",
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: /Save departments/i }).click();
    expect((await deptSave).ok()).toBeTruthy();
    await expect(page.getByText(/Departments saved/i).first()).toBeVisible({ timeout: 10_000 });

    const depts = await apiRequest(accessToken, "GET", "/hospital-profile/departments");
    const names = (Array.isArray(depts) ? depts : [])
      .map((d) => (typeof d === "string" ? d : d?.name))
      .filter(Boolean);
    expect(names).toContain(deptName);

    await page.getByRole("button", { name: `Remove ${deptName}` }).click();
    const restoreSave = page.waitForResponse(
      (r) => r.url().includes("/api/hospital-profile") && r.request().method() === "PUT",
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: /Save departments/i }).click();
    await restoreSave;
  });

  test("integrations page forbidden for reception", async ({ page }) => {
    const users = await ensureTestUsers(stamp);
    const { user } = await apiLogin(users.reception.email, users.reception.password);
    const perms = getPermissionsForUser(user);
    expect(perms.includes(PERMISSIONS.SettingsView), "reception fixture should lack Settings.View").toBeFalsy();

    await login(page, {
      email: users.reception.email,
      password: users.reception.password,
    });
    await page.goto("/settings/integrations", { waitUntil: "domcontentloaded" });

    // RequirePermission redirects to / — wait for client navigation
    await expect(page).not.toHaveURL(/\/settings\/integrations(?:\/)?$/, { timeout: 15_000 });
    await expect(page.getByTestId("integrations-panel")).toHaveCount(0);
  });
});

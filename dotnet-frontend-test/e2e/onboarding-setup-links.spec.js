const { test, expect } = require("@playwright/test");
const { registerTenant, platformApproveTenant, apiLogin } = require("./helpers/api");

const runId = Date.now();
const hospitalName = `Onboarding Setup ${runId}`;
const adminEmail = `onboarding-setup-${runId}@e2e.cureflow.test`;
const adminPassword = "Test@12345!";

const ONBOARDING_LINKS = [
  {
    key: "profile",
    href: "/settings/hospital",
    expectTestId: "departments-heading",
    label: "Open hospital settings",
  },
  {
    key: "whatsapp",
    href: "/settings/integrations",
    expectTestId: "integrations-panel",
    label: "Open integrations",
  },
  {
    key: "team",
    href: "/settings/users",
    expectTestId: "users-search",
    label: "Manage users",
  },
];

test.describe.configure({ mode: "serial" });

test.describe("Onboarding setup links and screens", () => {
  let tenantId;

  test.beforeAll(async () => {
    const tenant = await registerTenant({
      hospitalName,
      adminName: "Setup Admin",
      adminEmail,
      adminPassword,
    });
    tenantId = tenant.id;
    await platformApproveTenant(tenantId);
  });

  async function seedOnboardingSession(page) {
    const session = await apiLogin(adminEmail, adminPassword);
    expect(session.tenant.onboarding_complete).toBe(false);

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
  }

  test("onboarding page shows all setup steps and actions", async ({ page }) => {
    await seedOnboardingSession(page);

    await expect(page.getByTestId("onboarding-step-profile")).toBeVisible();
    await expect(page.getByTestId("onboarding-step-whatsapp")).toBeVisible();
    await expect(page.getByTestId("onboarding-step-team")).toBeVisible();
    await expect(page.getByTestId("onboarding-complete-profile")).toBeVisible();
    await expect(page.getByTestId("onboarding-complete-whatsapp")).toBeVisible();
    await expect(page.getByTestId("onboarding-complete-team")).toBeVisible();
    await expect(page.getByTestId("onboarding-finish")).toBeVisible();
  });

  for (const link of ONBOARDING_LINKS) {
    test(`link ${link.key} opens ${link.href} and stays on settings screen`, async ({ page }) => {
      await seedOnboardingSession(page);

      await page.getByTestId(`onboarding-link-${link.key}`).click();
      await page.waitForURL(new RegExp(`${link.href.replace("/", "\\/")}$`), { timeout: 20_000 });

      await expect(page.getByTestId("onboarding-setup-banner")).toBeVisible({ timeout: 20_000 });
      await expect(page.getByTestId(link.expectTestId)).toBeVisible({ timeout: 20_000 });
      await expect(page).not.toHaveURL(/\/onboarding$/);
    });

    test(`back to setup from ${link.href} returns to onboarding`, async ({ page }) => {
      await seedOnboardingSession(page);

      await page.getByTestId(`onboarding-link-${link.key}`).click();
      await page.waitForURL(new RegExp(`${link.href.replace("/", "\\/")}$`), { timeout: 20_000 });
      await expect(page.getByTestId(link.expectTestId)).toBeVisible({ timeout: 20_000 });

      await page.getByTestId("onboarding-back-link").click();
      await page.waitForURL(/\/onboarding$/, { timeout: 20_000 });
      await expect(page.getByTestId("onboarding-title")).toBeVisible();
    });
  }

  test("CRM routes remain blocked until onboarding completes", async ({ page }) => {
    await seedOnboardingSession(page);

    await page.goto("/patients", { waitUntil: "domcontentloaded" });
    await page.waitForURL(/\/onboarding/, { timeout: 20_000 });

    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await page.waitForURL(/\/onboarding/, { timeout: 20_000 });
  });

  test("mark done and finish onboarding unlocks dashboard", async ({ page }) => {
    await seedOnboardingSession(page);

    await page.getByTestId("onboarding-complete-profile").click();
    await page.getByTestId("onboarding-complete-whatsapp").click();
    await page.getByTestId("onboarding-complete-team").click();
    await page.getByTestId("onboarding-finish").click();

    await page.waitForURL((url) => url.pathname === "/", { timeout: 20_000 });
    await page.goto("/patients", { waitUntil: "domcontentloaded" });
    await expect(page).toHaveURL(/\/patients/, { timeout: 20_000 });
  });
});

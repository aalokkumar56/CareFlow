const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const {
  apiLogin,
  createPatient,
  markAllNotificationsRead,
  createNurseUser,
  createUser,
  ensureNotificationTypesEnabled,
} = require("./helpers/api");

// In the fan-out-on-read model a user never sees an event they created. The viewer (admin)
// must therefore NOT be the creator, so patients are authored by a separate receptionist user.
// Reuses the caller's admin token to avoid an extra login (the auth endpoint is rate-limited).
async function getAuthorToken(adminToken) {
  const email = "e2e.notif.author@cureflow.in";
  const password = "author12345";
  try {
    // "reception" is the legacy UserRole that maps to the Receptionist RBAC role (has Patient.Create).
    await createUser(adminToken, { name: "E2E Author", email, password, role: "reception" });
  } catch {
    // user may already exist from a prior run
  }
  const { accessToken } = await apiLogin(email, password);
  return accessToken;
}

test.describe("Notifications bell dropdown", () => {
  test.beforeAll(async () => {
    const { accessToken } = await apiLogin("admin@cureflow.in", "admin123");
    await ensureNotificationTypesEnabled(accessToken);
    await markAllNotificationsRead(accessToken);
  });

  test("bell badge, unread/read styling, scroll, and preferences link", async ({ page }) => {
    const { accessToken } = await apiLogin("admin@cureflow.in", "admin123");
    await ensureNotificationTypesEnabled(accessToken);
    await markAllNotificationsRead(accessToken);

    // Author the patients as a different user so the admin viewer (not the creator) sees them.
    const authorToken = await getAuthorToken(accessToken);
    const suffix = Date.now();
    await createPatient(authorToken, `E2E Patient ${suffix}`);

    for (let i = 0; i < 12; i += 1) {
      await createPatient(authorToken, `Scroll Patient ${suffix}-${i}`);
    }

    await login(page);
    await page.waitForLoadState("networkidle");

    const bell = page.getByTestId("notification-bell-desktop").first();
    await expect(bell).toBeVisible();
    await expect(page.getByTestId("notification-badge").first()).toBeVisible({ timeout: 20_000 });

    await bell.click();
    await expect(page.getByTestId("notification-dropdown")).toBeVisible();

    const unreadItem = page.locator('[data-testid^="notification-item-"][data-read="false"]').first();
    await expect(unreadItem).toBeVisible();
    await expect(unreadItem.locator('[data-testid="notification-unread-dot"]')).toBeVisible();

    const scrollContainer = page.getByTestId("notification-list-scroll");
    const { scrollHeight, clientHeight } = await scrollContainer.evaluate((el) => ({
      scrollHeight: el.scrollHeight,
      clientHeight: el.clientHeight,
    }));
    expect(scrollHeight).toBeGreaterThan(clientHeight);

    await scrollContainer.evaluate((el) => {
      el.scrollTop = el.scrollHeight;
    });

    const firstUnreadId = await unreadItem.getAttribute("data-testid");
    await unreadItem.click();

    await page.waitForTimeout(500);
    await bell.click();
    const readItem = page.getByTestId(firstUnreadId);
    await expect(readItem).toHaveAttribute("data-read", "true");
    await expect(readItem.locator('[data-testid="notification-unread-dot"]')).toHaveCount(0);
  });

  test("user without Settings access can open preferences from bell", async ({ page }) => {
    const nurseEmail = `nurse.e2e.${Date.now()}@cureflow.in`;
    const nursePassword = "nurse12345";

    const { accessToken } = await apiLogin("admin@cureflow.in", "admin123");
    try {
      await createNurseUser(accessToken, nurseEmail, nursePassword);
    } catch {
      // user may already exist from a prior run
    }

    const { accessToken: nurseToken } = await apiLogin(nurseEmail, nursePassword);
    await ensureNotificationTypesEnabled(nurseToken, ["patient.created"]);

    await login(page, { email: nurseEmail, password: nursePassword });
    await page.waitForLoadState("networkidle");

    await page.getByTestId("notification-bell-desktop").first().click();
    await page.getByTestId("notification-preferences-link").click();

    await expect(page).toHaveURL(/\/notifications\/preferences/);
    await expect(page.getByTestId("notification-preferences-title")).toBeVisible();
    await expect(page.getByRole("button", { name: "Save preferences" })).toBeVisible();

    await expect(page.getByRole("heading", { name: "Patients", exact: true })).toBeVisible();
  });
});

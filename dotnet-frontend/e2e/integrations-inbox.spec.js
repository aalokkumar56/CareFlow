const { test } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin } = require("./helpers/api");
const { ensureTestUsers } = require("./helpers/test-users");
const { getPermissionsForUser, PERMISSIONS } = require("../src/lib/permissions");
const { reportBug } = require("./helpers/bug-log");
const { setupConsoleCapture } = require("./helpers/ui-audit");

test.describe.configure({ mode: "serial" });

test.describe("Integrations and inbox flows", () => {
  test.beforeAll(async () => {
    await ensureTestUsers(Date.now());
  });

  test("admin: WhatsApp inbox loads with search and conversation list", async ({ page }) => {
    const capture = setupConsoleCapture(page);
    await login(page);
    await page.goto("/inbox", { waitUntil: "networkidle" });

    if (page.url().includes("/login")) {
      reportBug({
        severity: "Critical",
        page: "/inbox",
        title: "Admin redirected from WhatsApp inbox",
      });
      return;
    }

    const search = page.getByTestId("inbox-search");
    if (!(await search.isVisible().catch(() => false))) {
      reportBug({
        severity: "High",
        page: "/inbox",
        title: "Inbox search missing",
        expected: "inbox-search visible",
      });
    } else {
      await search.fill("test");
      await page.waitForTimeout(400);
    }

    const convCount = await page.locator("[data-testid^='conv-']").count();
    if (convCount === 0) {
      reportBug({
        severity: "Low",
        page: "/inbox",
        title: "No conversations in inbox list",
        actual: "Empty list — may be expected in fresh env",
      });
    }

    const errors = capture.getFilteredErrors();
    if (errors.length) {
      reportBug({
        severity: "Medium",
        page: "/inbox",
        title: "Console errors on inbox page",
        actual: errors.slice(0, 3).join("; "),
      });
    }
  });

  test("admin: email inbox loads", async ({ page }) => {
    await login(page);
    await page.goto("/email-inbox", { waitUntil: "networkidle" });

    if (page.url().includes("/login")) {
      reportBug({
        severity: "Critical",
        page: "/email-inbox",
        title: "Admin redirected from email inbox",
      });
      return;
    }

    const bodyText = await page.locator("body").textContent();
    const hasContent = bodyText && bodyText.length > 100;
    if (!hasContent) {
      reportBug({
        severity: "High",
        page: "/email-inbox",
        title: "Email inbox page appears empty",
      });
    }

    const disabledBanner = page.getByText(/email.*disabled|not configured|enable email/i);
    const threads = page.locator("[class*='thread'], table, [role='list']").first();
    const hasBanner = await disabledBanner.isVisible().catch(() => false);
    const hasThreads = await threads.isVisible().catch(() => false);

    if (!hasBanner && !hasThreads) {
      reportBug({
        severity: "Medium",
        page: "/email-inbox",
        title: "Email inbox shows neither threads nor disabled banner",
        expected: "Thread list or configuration banner",
      });
    }
  });

  test("admin: settings integrations shows WhatsApp/SMS/Email panels", async ({ page }) => {
    await login(page);
    await page.goto("/settings/integrations", { waitUntil: "networkidle" });

    const panel = page.getByTestId("integrations-panel");
    if (!(await panel.isVisible().catch(() => false))) {
      reportBug({
        severity: "High",
        page: "/settings/integrations",
        title: "Integrations panel missing",
        expected: "integrations-panel visible",
      });
      return;
    }

    const whatsappSection = page.getByText("WhatsApp Business");
    const smsSection = page.getByText(/SMS/i).first();
    const emailSection = page.getByText(/Email/i).first();

    if (!(await whatsappSection.isVisible().catch(() => false))) {
      reportBug({
        severity: "High",
        page: "/settings/integrations",
        title: "WhatsApp config section missing",
      });
    }
    if (!(await smsSection.isVisible().catch(() => false))) {
      reportBug({
        severity: "Medium",
        page: "/settings/integrations",
        title: "SMS config section missing",
      });
    }
    if (!(await emailSection.isVisible().catch(() => false))) {
      reportBug({
        severity: "Medium",
        page: "/settings/integrations",
        title: "Email config section missing",
      });
    }

    const configureBtns = page.getByRole("button", { name: /configure/i });
    if ((await configureBtns.count()) === 0) {
      reportBug({
        severity: "Low",
        page: "/settings/integrations",
        title: "No Configure buttons on integrations page",
      });
    }
  });

  test("reception: inbox accessible, integrations not", async ({ page }) => {
    const stamp = Date.now();
    const users = await ensureTestUsers(stamp);
    const { user } = await apiLogin(users.reception.email, users.reception.password);
    const perms = getPermissionsForUser(user);

    await login(page, { email: users.reception.email, password: users.reception.password });

    if (perms.includes(PERMISSIONS.ConversationView)) {
      await page.goto("/inbox", { waitUntil: "networkidle" });
      if (new URL(page.url()).pathname !== "/inbox") {
        reportBug({
          severity: "High",
          page: "/inbox",
          title: "Reception cannot access inbox",
          expected: "/inbox",
          actual: page.url(),
        });
      }
    }

    await page.goto("/settings/integrations", { waitUntil: "networkidle" });
    if (!perms.includes(PERMISSIONS.SettingsView) && new URL(page.url()).pathname === "/settings/integrations") {
      reportBug({
        severity: "Critical",
        page: "/settings/integrations",
        title: "Reception accessed integrations settings",
      });
    }
  });

  test("notification bell and command palette on dashboard", async ({ page }) => {
    await login(page);
    await page.goto("/", { waitUntil: "networkidle" });

    const bell = page.getByTestId("notification-bell-desktop").first();
    if (!(await bell.isVisible().catch(() => false))) {
      reportBug({
        severity: "Medium",
        page: "/",
        title: "Notification bell not visible on dashboard",
      });
    } else {
      await bell.click();
      const dropdown = page.getByTestId("notification-dropdown");
      if (!(await dropdown.isVisible().catch(() => false))) {
        reportBug({
          severity: "Medium",
          page: "/",
          title: "Notification dropdown does not open on dashboard",
        });
      }
      await page.keyboard.press("Escape");
    }

    const palette = page.getByTestId("open-command-palette").or(page.getByTestId("open-command-palette-mobile"));
    if (!(await palette.first().isVisible().catch(() => false))) {
      reportBug({
        severity: "Medium",
        page: "/",
        title: "Command palette not available on dashboard",
      });
    }
  });

  test("patients page: bell visible, command palette hidden (documented pattern)", async ({ page }) => {
    await login(page);
    await page.goto("/patients", { waitUntil: "networkidle" });

    const bell = page.getByTestId("notification-bell-desktop").first();
    const palette = page.getByTestId("open-command-palette");

    const bellVisible = await bell.isVisible().catch(() => false);
    const paletteVisible = await palette.isVisible().catch(() => false);

    if (!bellVisible) {
      reportBug({
        severity: "Medium",
        page: "/patients",
        title: "Notification bell hidden on patients list",
        expected: "Visible (Patients only hides hideHeaderSearch)",
      });
    }
    if (paletteVisible) {
      reportBug({
        severity: "Low",
        page: "/patients",
        title: "Command palette visible on patients despite hideHeaderSearch",
      });
    }
  });

  test("notifications preferences standalone page", async ({ page }) => {
    await login(page);
    await page.goto("/notifications/preferences", { waitUntil: "networkidle" });

    const title = page.getByTestId("notification-preferences-title");
    if (!(await title.isVisible().catch(() => false))) {
      reportBug({
        severity: "High",
        page: "/notifications/preferences",
        title: "Notification preferences page failed to load",
      });
    }

    const dashLink = page.locator('a[href="/dashboard"]');
    if (await dashLink.count() > 0) {
      await dashLink.first().click();
      await page.waitForTimeout(500);
      const path = new URL(page.url()).pathname;
      if (path === "/dashboard") {
        reportBug({
          severity: "High",
          page: "/notifications/preferences",
          title: "Broken /dashboard breadcrumb link",
          steps: "Click dashboard link in preferences breadcrumb",
          expected: "Navigate to /",
          actual: "URL is /dashboard (no route)",
        });
      }
    }
  });
});

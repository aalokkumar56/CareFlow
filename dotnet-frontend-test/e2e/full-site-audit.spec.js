const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin, apiRequest, createPatient } = require("./helpers/api");
const { ensureTestUsers, ROLES } = require("./helpers/test-users");
const { MAIN_ROUTES, SETTINGS_ROUTES, PATIENT_TABS, CLINICAL_TABS, NAV_ITEMS } = require("./helpers/routes");
const { auditPageUI } = require("./helpers/ui-audit");
const { ROLE_PERMISSIONS, normalizeRole } = require("../../dotnet-frontend/src/lib/permissions");

const bugs = [];

function reportBug(category, severity, title, details) {
  bugs.push({ category, severity, title, details });
}

test.describe.configure({ mode: "serial" });

test.describe("Full-site functional & UI audit", () => {
  let testUsers;
  let adminToken;
  let samplePatientId;
  let sampleDoctorId;
  let sampleCampaignId;

  test.beforeAll(async () => {
    const stamp = Date.now();
    testUsers = await ensureTestUsers(stamp);
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    adminToken = admin.accessToken;

    const patient = await createPatient(adminToken, `Audit Patient ${stamp}`, `91${String(stamp).slice(-8)}`);
    samplePatientId = patient.id || patient.patient?.id;

    try {
      const doctors = await apiRequest(adminToken, "GET", "/doctors");
      const list = Array.isArray(doctors) ? doctors : doctors?.items || doctors?.data || [];
      if (list.length > 0) sampleDoctorId = list[0].id;
    } catch { /* optional */ }

    try {
      const campaigns = await apiRequest(adminToken, "GET", "/campaigns");
      const list = Array.isArray(campaigns) ? campaigns : campaigns?.items || campaigns?.data || [];
      if (list.length > 0) sampleCampaignId = list[0].id;
    } catch { /* optional */ }
  });

  test.afterAll(async () => {
    console.log("\n========== BUG REPORT ==========\n");
    if (bugs.length === 0) {
      console.log("No bugs recorded during audit.");
    } else {
      bugs.forEach((b, i) => {
        console.log(`${i + 1}. [${b.severity}] [${b.category}] ${b.title}`);
        console.log(`   ${b.details}\n`);
      });
      console.log(`Total bugs: ${bugs.length}`);
    }
  });

  test("admin: crawl all main routes", async ({ page }) => {
    const apiFailures = [];
    page.on("response", (res) => {
      if (res.url().includes("/api/") && res.status() >= 500) {
        apiFailures.push(`${res.status()} ${res.url()}`);
      }
    });

    await login(page);
    for (const route of MAIN_ROUTES) {
      await page.goto(route.path, { waitUntil: "networkidle" });
      await page.waitForTimeout(500);

      const url = page.url();
      if (url.includes("/login")) {
        reportBug("Functional", "Critical", `Admin blocked from ${route.label}`, `Navigated to ${route.path} but redirected to login`);
        continue;
      }

      if (route.expectTestId) {
        const el = page.getByTestId(route.expectTestId);
        const visible = await el.isVisible().catch(() => false);
        if (!visible) {
          reportBug("Functional", "High", `${route.label} missing expected element`, `data-testid="${route.expectTestId}" not visible at ${route.path}`);
        }
      }

      const ui = await auditPageUI(page, route.label);
      ui.issues.forEach((issue) => reportBug("UI", "Medium", issue.split(":")[0], issue));

    }

    if (apiFailures.length) {
      reportBug("Functional", "High", "API 5xx errors during main route crawl", apiFailures.join("; "));
    }
  });

  test("admin: crawl all settings routes", async ({ page }) => {
    await login(page);
    for (const route of SETTINGS_ROUTES) {
      await page.goto(route.path, { waitUntil: "networkidle" });
      await page.waitForTimeout(400);

      if (page.url().includes("/login")) {
        reportBug("Functional", "Critical", `Admin blocked from ${route.label}`, route.path);
        continue;
      }

      if (route.expectTestId) {
        const visible = await page.getByTestId(route.expectTestId).isVisible().catch(() => false);
        if (!visible) {
          reportBug("Functional", "High", `${route.label} missing expected element`, route.path);
        }
      }

      const ui = await auditPageUI(page, route.label);
      ui.issues.forEach((issue) => reportBug("UI", "Medium", route.label, issue));
    }
  });

  test("admin: patient detail — all tabs", async ({ page }) => {
    if (!samplePatientId) {
      test.skip();
      return;
    }

    await login(page);
    await page.goto(`/patients/${samplePatientId}`, { waitUntil: "networkidle" });

    const nameVisible = await page.getByTestId("patient-name").isVisible().catch(() => false);
    if (!nameVisible) {
      reportBug("Functional", "Critical", "Patient detail page failed to load", `/patients/${samplePatientId}`);
      return;
    }

    const pageTitleCount = await page.getByTestId("page-title").count();
    if (pageTitleCount > 0) {
      reportBug("UI", "Medium", "Duplicate patient header", "page-title h1 should not appear when breadcrumb header is used on patient detail");
    }

    const breadcrumbVisible = await page.getByTestId("page-breadcrumb").isVisible().catch(() => false);
    if (!breadcrumbVisible) {
      reportBug("UI", "Medium", "Patient detail missing breadcrumb", "Expected data-testid=page-breadcrumb in AppShell header");
    }

    const patientNameText = await page.getByTestId("patient-name").textContent();
    if (patientNameText && /^\d{6}\.\.\.$/.test(patientNameText.trim())) {
      reportBug("UI", "High", "Patient name truncated in primary display", patientNameText);
    }

    const backLink = page.getByTestId("patient-back-link");
    if (!(await backLink.isVisible().catch(() => false))) {
      reportBug("UI", "Medium", "Patient detail missing back link", "Expected data-testid=patient-back-link");
    }

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2);
    if (overflow) {
      reportBug("UI", "Medium", "Patient detail horizontal overflow", samplePatientId);
    }

    for (const tabId of PATIENT_TABS) {
      const tab = page.getByTestId(tabId);
      const exists = await tab.count();
      if (exists === 0) {
        if (CLINICAL_TABS.includes(tabId)) continue;
        reportBug("Functional", "Medium", `Patient tab missing: ${tabId}`, "Admin should see all tabs");
        continue;
      }

      await tab.click();
      await page.waitForTimeout(400);

      const consoleErrors = [];
      page.on("console", (msg) => {
        if (msg.type() === "error") consoleErrors.push(msg.text());
      });

      const tabContent = page.locator('[role="tabpanel"]:visible, [data-state="active"]');
      const hasContent = (await tabContent.count()) > 0;
      if (!hasContent && tabId !== "tab-timeline") {
        reportBug("Functional", "Medium", `Patient tab ${tabId} shows no content panel`, samplePatientId);
      }
    }
  });

  test("admin: doctor detail tabs", async ({ page }) => {
    if (!sampleDoctorId) {
      test.skip();
      return;
    }

    await login(page);
    await page.goto(`/doctors/${sampleDoctorId}`, { waitUntil: "networkidle" });

    const nameVisible = await page.getByTestId("doctor-name").isVisible().catch(() => false);
    if (!nameVisible) {
      reportBug("Functional", "High", "Doctor detail page failed to load", `/doctors/${sampleDoctorId}`);
      return;
    }

    await page.getByRole("tab", { name: "Referrals" }).click().catch(() => {});
    await page.waitForTimeout(300);
    const logBtn = page.getByTestId("log-referral-btn");
    if (!(await logBtn.isVisible().catch(() => false))) {
      reportBug("Functional", "Medium", "Doctor detail Referrals tab missing log button", sampleDoctorId);
    }
  });

  test("admin: campaign detail page", async ({ page }) => {
    if (!sampleCampaignId) {
      test.skip();
      return;
    }

    await login(page);
    await page.goto(`/campaigns/${sampleCampaignId}`, { waitUntil: "networkidle" });

    const title = page.locator("h1, h2").first();
    if (!(await title.isVisible().catch(() => false))) {
      reportBug("Functional", "High", "Campaign detail page failed to load", `/campaigns/${sampleCampaignId}`);
    }
  });

  test("broken /dashboard route link", async ({ page }) => {
    await login(page);
    await page.goto("/notifications/preferences", { waitUntil: "networkidle" });

    const dashLink = page.locator('a[href="/dashboard"]');
    if (await dashLink.count() > 0) {
      await dashLink.first().click();
      await page.waitForTimeout(500);
      const path = new URL(page.url()).pathname;
      if (path === "/dashboard") {
        reportBug("Functional", "High", "Broken /dashboard route", "Notification preferences links to /dashboard which has no route — catch-all redirects to / but URL may flash");
      }
    }
  });

  test("login page UI and form validation", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByTestId("login-email")).toBeVisible();
    await expect(page.getByTestId("login-password")).toBeVisible();

    await page.getByTestId("login-submit").click();
    await page.waitForTimeout(500);

    const stillOnLogin = page.url().includes("/login");
    if (!stillOnLogin) {
      reportBug("Functional", "Medium", "Login allows empty credentials", "Submitting empty login form did not stay on login page");
    }

    const ui = await auditPageUI(page, "Login");
    ui.issues.forEach((issue) => reportBug("UI", "Low", "Login", issue));
  });

  test("invalid login shows error", async ({ page }) => {
    await page.goto("/login");
    await page.getByTestId("login-email").fill("invalid@test.com");
    await page.getByTestId("login-password").fill("wrongpassword");

    const loginResponse = page.waitForResponse(
      (r) => r.url().includes("/api/auth/login") && r.request().method() === "POST",
      { timeout: 15_000 },
    );
    await page.getByTestId("login-submit").click();
    const res = await loginResponse;

    if (res.ok()) {
      reportBug("Functional", "Critical", "Invalid credentials accepted", "Login succeeded with wrong password");
    }

    const errorVisible = await page.locator('[role="alert"], .text-destructive, [data-sonner-toast]').first().isVisible().catch(() => false);
    const stillOnLogin = page.url().includes("/login");
    if (!errorVisible && stillOnLogin) {
      reportBug("UI", "Low", "No visible error on failed login", "User may not know login failed");
    }
  });

  for (const role of ROLES) {
    test(`role ${role}: navigation visibility matches permissions`, async ({ page }) => {
      const creds = testUsers[role];
      await login(page, { email: creds.email, password: creds.password });

      const perms = ROLE_PERMISSIONS[normalizeRole(role)] || [];

      for (const nav of NAV_ITEMS) {
        const navEl = page.getByTestId(nav.testId);
        const shouldSee = nav.anyPermission
          ? nav.anyPermission.some((p) => perms.includes(p))
          : perms.includes(nav.permission);
        const visible = await navEl.isVisible().catch(() => false);

        if (shouldSee && !visible) {
          reportBug("Functional", "High", `${role}: missing nav item ${nav.testId}`, `Expected visible based on ROLE_PERMISSIONS`);
        }
        if (!shouldSee && visible) {
          reportBug("Functional", "High", `${role}: unauthorized nav item visible ${nav.testId}`, `Should be hidden for role ${role}`);
        }
      }
    });

    test(`role ${role}: route access enforcement`, async ({ page }) => {
      const creds = testUsers[role];
      await login(page, { email: creds.email, password: creds.password });
      const perms = ROLE_PERMISSIONS[normalizeRole(role)] || [];

      const allRoutes = [...MAIN_ROUTES, ...SETTINGS_ROUTES.filter((r) => r.path !== "/notifications/preferences")];

      for (const route of allRoutes) {
        const allowed = route.anyPermission
          ? route.anyPermission.some((p) => perms.includes(p))
          : route.permission
            ? perms.includes(route.permission)
            : true;

        await page.goto(route.path, { waitUntil: "domcontentloaded" });
        await page.waitForTimeout(400);

        const pathname = new URL(page.url()).pathname;
        const redirectedAway = pathname !== route.path && pathname !== "/login";

        if (!allowed && pathname === route.path) {
          reportBug("Functional", "Critical", `${role} accessed forbidden route`, `${route.path} should redirect but stayed on page`);
        }

        if (!allowed && redirectedAway && pathname === "/") {
          // expected — redirected to dashboard
        } else if (!allowed && pathname === route.path) {
          reportBug("Functional", "Critical", `${role} permission bypass`, `Can view ${route.label} at ${route.path}`);
        }
      }
    });
  }

  test("nurse: patient clinical tabs visibility", async ({ page }) => {
    const creds = testUsers.nurse;
    if (!samplePatientId) {
      test.skip();
      return;
    }

    await login(page, { email: creds.email, password: creds.password });
    await page.goto(`/patients/${samplePatientId}`, { waitUntil: "networkidle" });

    for (const tabId of CLINICAL_TABS) {
      const tab = page.getByTestId(tabId);
      const visible = await tab.isVisible().catch(() => false);
      if (!visible) {
        reportBug("Functional", "High", "Nurse missing clinical tab", `${tabId} should be visible for nurse role`);
      }
    }

    const whatsappIcon = page.getByTestId("patient-whatsapp-icon");
    if (await whatsappIcon.isVisible().catch(() => false)) {
      reportBug("Functional", "Medium", "Nurse sees WhatsApp icon without permission", "Nurse role lacks WhatsApp.View/Send");
    }
  });

  test("reception: can create patient flow", async ({ page }) => {
    const creds = testUsers.reception;
    await login(page, { email: creds.email, password: creds.password });
    await page.goto("/patients");

    const newBtn = page.getByTestId("new-patient-btn");
    if (!(await newBtn.isVisible().catch(() => false))) {
      reportBug("Functional", "High", "Reception cannot see New Patient button", "Has Patient.Create permission");
      return;
    }

    await newBtn.click();
    const dialog = page.getByTestId("new-patient-dialog");
    if (!(await dialog.isVisible().catch(() => false))) {
      reportBug("Functional", "High", "Reception New Patient dialog failed to open", "");
    }
  });

  test("marketing: cannot access settings users", async ({ page }) => {
    const creds = testUsers.marketing;
    await login(page, { email: creds.email, password: creds.password });
    await page.goto("/settings/users");
    await page.waitForTimeout(500);

    const pathname = new URL(page.url()).pathname;
    if (pathname === "/settings/users") {
      reportBug("Functional", "Critical", "Marketing accessed user management", "Should lack User.View permission");
    }
  });

  test("UI: AppShell consistency across pages", async ({ page }) => {
    await login(page);
    const pagesToCheck = ["/patients", "/appointments", "/tasks", "/campaigns"];
    const headerStyles = [];

    for (const path of pagesToCheck) {
      await page.goto(path, { waitUntil: "networkidle" });
      const title = page.getByTestId("page-title");
      if (!(await title.isVisible().catch(() => false))) {
        reportBug("UI", "Medium", `Missing page-title on ${path}`, "AppShell should render data-testid=page-title");
        continue;
      }
      const style = await title.evaluate((el) => {
        const s = window.getComputedStyle(el);
        return { fontSize: s.fontSize, fontFamily: s.fontFamily, color: s.color, fontWeight: s.fontWeight };
      });
      headerStyles.push({ path, ...style });
    }

    const uniqueSizes = new Set(headerStyles.map((s) => s.fontSize));
    if (uniqueSizes.size > 1) {
      reportBug("UI", "Medium", "Inconsistent page title font sizes", headerStyles.map((s) => `${s.path}: ${s.fontSize}`).join(", "));
    }

    const uniqueColors = new Set(headerStyles.map((s) => s.color));
    if (uniqueColors.size > 1) {
      reportBug("UI", "Low", "Inconsistent page title colors", headerStyles.map((s) => `${s.path}: ${s.color}`).join(", "));
    }
  });

  test("UI: primary button styles consistency", async ({ page }) => {
    await login(page);
    const paths = ["/patients", "/appointments", "/tasks", "/campaigns", "/staff"];
    const primaryStyles = new Set();

    for (const path of paths) {
      await page.goto(path, { waitUntil: "networkidle" });
      const primaryBtn = page.locator(".btn-primary:visible").first();
      if (await primaryBtn.count() === 0) continue;
      const bg = await primaryBtn.evaluate((el) => window.getComputedStyle(el).backgroundColor);
      primaryStyles.add(bg);
    }

    if (primaryStyles.size > 2) {
      reportBug("UI", "Medium", "Inconsistent primary button colors", [...primaryStyles].join(", "));
    }
  });

  test("functional: command palette opens", async ({ page }) => {
    await login(page);
    await page.goto("/");
    const paletteBtn = page.getByTestId("open-command-palette").or(page.getByTestId("open-command-palette-mobile"));
    if (!(await paletteBtn.first().isVisible().catch(() => false))) {
      reportBug("Functional", "Medium", "Command palette button not visible", "/");
      return;
    }
    await paletteBtn.first().click();
    const input = page.getByTestId("cmd-palette-input");
    if (!(await input.isVisible().catch(() => false))) {
      reportBug("Functional", "High", "Command palette does not open", "");
    }
  });

  test("functional: notification bell", async ({ page }) => {
    await login(page);
    await page.goto("/patients");
    const bell = page.getByTestId("notification-bell-desktop").first();
    if (!(await bell.isVisible().catch(() => false))) {
      reportBug("Functional", "Medium", "Notification bell not visible", "Expected on standard AppShell pages");
      return;
    }
    await bell.click();
    const dropdown = page.getByTestId("notification-dropdown");
    if (!(await dropdown.isVisible().catch(() => false))) {
      reportBug("Functional", "Medium", "Notification dropdown does not open", "");
    }
  });
});

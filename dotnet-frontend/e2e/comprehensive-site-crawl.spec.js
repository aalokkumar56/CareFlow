const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin, apiRequest, createPatient } = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");
const { ensureTestUsers } = require("./helpers/test-users");
const {
  MAIN_ROUTES,
  SETTINGS_ROUTES,
  PATIENT_TABS,
  PATIENT_EDIT_TABS,
  CLINICAL_TABS,
} = require("./helpers/routes");
const {
  auditPageUI,
  setupConsoleCapture,
  auditPageHeading,
  countIconButtonsWithoutAriaLabel,
} = require("./helpers/ui-audit");
const { reportBug } = require("./helpers/bug-log");

test.describe.configure({ mode: "serial" });

test.describe("Comprehensive site crawl", () => {
  let samplePatientId;
  let sampleDoctorId;
  let sampleCampaignId;

  test.beforeAll(async () => {
    const stamp = Date.now();
    await ensureTestUsers(stamp);
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const patient = await createPatient(admin.accessToken, `Crawl Patient ${stamp}`, E2E_TEST_PHONE);
    samplePatientId = patient.id || patient.patient?.id;

    try {
      const doctors = await apiRequest(admin.accessToken, "GET", "/doctors");
      const list = Array.isArray(doctors) ? doctors : doctors?.items || doctors?.data || [];
      if (list.length > 0) sampleDoctorId = list[0].id;
    } catch { /* optional */ }

    try {
      const campaigns = await apiRequest(admin.accessToken, "GET", "/campaigns");
      const list = Array.isArray(campaigns) ? campaigns : campaigns?.items || campaigns?.data || [];
      if (list.length > 0) sampleCampaignId = list[0].id;
    } catch { /* optional */ }
  });

  test("admin: crawl login and all main routes with dialogs", async ({ page }) => {
    const consoleCapture = setupConsoleCapture(page);
    const apiFailures = [];
    page.on("response", (res) => {
      if (res.url().includes("/api/") && res.status() >= 500) {
        apiFailures.push(`${res.status()} ${res.url()}`);
      }
    });

    await page.goto("/login", { waitUntil: "networkidle" });
    const loginHeading = await auditPageHeading(page);
    if (!loginHeading.hasPageTitle && loginHeading.h1Count === 0) {
      reportBug({
        severity: "Medium",
        page: "/login",
        title: "Login page missing heading",
        actual: "No page-title or h1 found",
      });
    }

    await login(page);

    for (const route of MAIN_ROUTES) {
      await page.goto(route.path, { waitUntil: "networkidle" });
      await page.waitForTimeout(400);

      if (page.url().includes("/login")) {
        reportBug({
          severity: "Critical",
          page: route.path,
          title: `Admin redirected to login from ${route.label}`,
          steps: `Navigate to ${route.path}`,
          expected: "Page loads",
          actual: "Redirected to /login",
        });
        continue;
      }

      const heading = await auditPageHeading(page);
      if (!heading.hasPageTitle && !heading.hasBreadcrumb && route.path !== "/") {
        reportBug({
          severity: "Medium",
          page: route.path,
          title: "Missing page-title or breadcrumb",
          expected: "page-title or page-breadcrumb visible",
          actual: "Neither found",
        });
      }

      if (route.expectTestId) {
        const visible = await page.getByTestId(route.expectTestId).isVisible().catch(() => false);
        if (!visible) {
          reportBug({
            severity: "High",
            page: route.path,
            title: `Missing expected element: ${route.expectTestId}`,
            expected: `data-testid="${route.expectTestId}" visible`,
            actual: "Not visible",
          });
        }
      }

      const ui = await auditPageUI(page, route.label);
      ui.issues.forEach((issue) => {
        const severity = issue.includes("overflow") || issue.includes("broken") ? "High" : "Medium";
        reportBug({ severity, page: route.path, title: issue.split(":")[0], actual: issue });
      });

      const icons = await countIconButtonsWithoutAriaLabel(page);
      if (icons.count > 5) {
        reportBug({
          severity: "Low",
          page: route.path,
          title: `${icons.count} icon-only buttons missing aria-label`,
          actual: icons.offenders.join(", "),
        });
      }
    }

    // Dialog smoke tests on key pages
    const dialogChecks = [
      { path: "/patients", btn: "new-patient-btn", dialog: "new-patient-dialog" },
      { path: "/appointments", btn: "new-appt-btn", dialog: "new-appt-dialog" },
      { path: "/campaigns", btn: "new-campaign-btn", dialog: "new-campaign-dialog" },
      { path: "/tasks", btn: "new-task-btn", dialog: null },
    ];

    for (const { path: p, btn, dialog } of dialogChecks) {
      await page.goto(p, { waitUntil: "networkidle" });
      const newBtn = page.getByTestId(btn);
      if (await newBtn.isVisible().catch(() => false)) {
        await newBtn.click();
        await page.waitForTimeout(300);
        if (dialog) {
          const open = await page.getByTestId(dialog).isVisible().catch(() => false);
          if (!open) {
            reportBug({
              severity: "High",
              page: p,
              title: `Dialog ${dialog} did not open`,
              steps: `Click ${btn}`,
              expected: "Dialog visible",
              actual: "Dialog not visible",
            });
          }
          await page.keyboard.press("Escape");
        }
      }
    }

    const errors = consoleCapture.getFilteredErrors();
    if (errors.length > 0) {
      reportBug({
        severity: "Medium",
        page: "main routes crawl",
        title: "Console errors during main route crawl",
        actual: errors.slice(0, 5).join("; "),
      });
    }

    if (apiFailures.length) {
      reportBug({
        severity: "High",
        page: "main routes crawl",
        title: "API 5xx errors during crawl",
        actual: apiFailures.join("; "),
      });
    }
  });

  test("admin: crawl all settings routes", async ({ page }) => {
    await login(page);

    await page.goto("/settings", { waitUntil: "networkidle" });
    const settingsPath = new URL(page.url()).pathname;
    if (settingsPath === "/settings") {
      reportBug({
        severity: "Medium",
        page: "/settings",
        title: "/settings did not redirect",
        expected: "Redirect to /settings/roles or /settings/integrations",
        actual: "Stayed on /settings",
      });
    }

    for (const route of SETTINGS_ROUTES) {
      await page.goto(route.path, { waitUntil: "networkidle" });
      await page.waitForTimeout(400);

      if (page.url().includes("/login")) {
        reportBug({
          severity: "Critical",
          page: route.path,
          title: `Admin blocked from ${route.label}`,
          actual: "Redirected to login",
        });
        continue;
      }

      if (route.expectTestId) {
        const visible = await page.getByTestId(route.expectTestId).isVisible().catch(() => false);
        if (!visible) {
          reportBug({
            severity: "High",
            page: route.path,
            title: `Missing ${route.expectTestId}`,
            actual: "Not visible",
          });
        }
      }

      if (route.path === "/settings/permissions") {
        const catalog = await page.getByTestId("permissions-catalog-panel").isVisible().catch(() => false);
        if (!catalog) {
          reportBug({
            severity: "High",
            page: route.path,
            title: "Permissions catalog panel missing",
            expected: "permissions-catalog-panel visible",
          });
        }
      }

      if (route.path === "/settings/roles") {
        const rolesPanel = await page.getByTestId("roles-permissions-panel").isVisible().catch(() => false);
        if (!rolesPanel) {
          reportBug({
            severity: "High",
            page: route.path,
            title: "Roles permissions matrix panel missing",
            expected: "roles-permissions-panel visible (distinct from permissions catalog)",
          });
        }
      }

      if (route.path === "/settings/notifications") {
        const dupCount = await page.getByRole("heading", { name: "Notifications", exact: true }).count();
        if (dupCount > 1) {
          reportBug({
            severity: "Medium",
            page: route.path,
            title: "Duplicate Notifications headings",
            actual: `${dupCount} headings with text Notifications`,
          });
        }

        const personalTab = page.getByRole("tab", { name: "My preferences", exact: true });
        const rolesTab = page.getByRole("tab", { name: "Role defaults", exact: true });
        if (!(await personalTab.isVisible().catch(() => false))) {
          reportBug({ severity: "High", page: route.path, title: "My preferences tab missing" });
        }
        if (!(await rolesTab.isVisible().catch(() => false))) {
          reportBug({ severity: "High", page: route.path, title: "Role defaults tab missing" });
        } else {
          await rolesTab.click();
          await page.waitForTimeout(300);
        }
        const saveRoles = await page.getByRole("button", { name: "Save role defaults" }).isVisible().catch(() => false);
        if (!saveRoles) {
          reportBug({ severity: "Medium", page: route.path, title: "Role defaults tab content missing save button" });
        }
      }

      const ui = await auditPageUI(page, route.label);
      ui.issues.forEach((issue) => {
        reportBug({ severity: "Medium", page: route.path, title: issue, actual: issue });
      });
    }
  });

  test("admin: patient detail all tabs, edit overlay, WhatsApp panel", async ({ page }) => {
    if (!samplePatientId) {
      test.skip();
      return;
    }

    await login(page);
    await page.goto(`/patients/${samplePatientId}`, { waitUntil: "networkidle" });

    if (!(await page.getByTestId("patient-name").isVisible().catch(() => false))) {
      reportBug({
        severity: "Critical",
        page: `/patients/${samplePatientId}`,
        title: "Patient detail failed to load",
      });
      return;
    }

    const pageTitleCount = await page.getByTestId("page-title").count();
    if (pageTitleCount > 0) {
      reportBug({
        severity: "Medium",
        page: `/patients/${samplePatientId}`,
        title: "Duplicate patient header in AppShell page-title",
        expected: "Only breadcrumb header, no page-title",
      });
    }

    for (const tabId of PATIENT_TABS) {
      const tab = page.getByTestId(tabId);
      if ((await tab.count()) === 0) {
        if (!CLINICAL_TABS.includes(tabId)) {
          reportBug({ severity: "Medium", page: "patient detail", title: `Tab missing: ${tabId}` });
        }
        continue;
      }
      await tab.click();
      await page.waitForTimeout(350);
      const panel = page.locator('[role="tabpanel"][data-state="active"]');
      if ((await panel.count()) === 0 && tabId !== "tab-timeline") {
        reportBug({ severity: "Medium", page: "patient detail", title: `Tab ${tabId} has no active panel` });
      }
    }

    await page.getByTestId("patient-edit-icon").click();
    await page.waitForTimeout(400);
    const editForm = page.getByTestId("edit-patient-form");
    if (!(await editForm.isVisible().catch(() => false))) {
      reportBug({
        severity: "High",
        page: "patient detail",
        title: "Edit patient overlay did not open",
        steps: "Click patient-edit-icon",
      });
    } else {
      for (const editTab of PATIENT_EDIT_TABS) {
        const et = page.getByTestId(editTab);
        if ((await et.count()) === 0) {
          reportBug({ severity: "Medium", page: "edit patient", title: `Edit sub-tab missing: ${editTab}` });
          continue;
        }
        await et.click();
        await page.waitForTimeout(200);
      }
      await page.keyboard.press("Escape");
    }

    const waIcon = page.getByTestId("patient-whatsapp-icon");
    if (await waIcon.isVisible().catch(() => false)) {
      await waIcon.click();
      await page.waitForTimeout(400);
      const waTitle = page.getByRole("heading", { name: "WhatsApp" });
      if (!(await waTitle.isVisible().catch(() => false))) {
        reportBug({
          severity: "Medium",
          page: "patient detail",
          title: "WhatsApp panel did not open for admin",
          steps: "Click patient-whatsapp-icon",
        });
      }
      await page.keyboard.press("Escape");
    }

    await page.getByTestId("patient-back-link").click();
    await page.waitForURL(/\/patients\/?$/, { timeout: 15_000 }).catch(() => {
      reportBug({
        severity: "Medium",
        page: "patient detail",
        title: "Back link did not navigate to patients list",
      });
    });
  });

  test("admin: doctor detail and campaign detail", async ({ page }) => {
    await login(page);

    if (sampleDoctorId) {
      await page.goto(`/doctors/${sampleDoctorId}`, { waitUntil: "networkidle" });
      if (!(await page.getByTestId("doctor-name").isVisible().catch(() => false))) {
        reportBug({
          severity: "High",
          page: `/doctors/${sampleDoctorId}`,
          title: "Doctor detail failed to load",
        });
      } else {
        await page.getByRole("tab", { name: "Referrals" }).click().catch(() => {});
        await page.waitForTimeout(300);
        const logBtn = page.getByTestId("log-referral-btn");
        if (await logBtn.isVisible().catch(() => false)) {
          await logBtn.click();
          await page.waitForTimeout(300);
          const dialog = page.getByTestId("ref-patient-select");
          if (!(await dialog.isVisible().catch(() => false))) {
            reportBug({
              severity: "Medium",
              page: "doctor detail",
              title: "Log referral dialog did not open",
            });
          }
          await page.keyboard.press("Escape");
        }
      }
    }

    if (sampleCampaignId) {
      await page.goto(`/campaigns/${sampleCampaignId}`, { waitUntil: "networkidle" });
      const title = page.locator("h1, h2").first();
      if (!(await title.isVisible().catch(() => false))) {
        reportBug({
          severity: "High",
          page: `/campaigns/${sampleCampaignId}`,
          title: "Campaign detail failed to load",
        });
      }
    }
  });

  test("invalid login rejected", async ({ page }) => {
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
      reportBug({
        severity: "Critical",
        page: "/login",
        title: "Invalid credentials accepted",
        expected: "401/403 response",
        actual: "Login succeeded",
      });
    }
  });
});

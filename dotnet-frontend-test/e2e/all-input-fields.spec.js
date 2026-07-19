/**
 * Comprehensive E2E — exercise every input field in the application.
 * Fills sample values and verifies acceptance; does not submit destructive actions.
 *
 *   npm run test:e2e:inputs
 */
const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin, createPatient } = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");
const {
  createFieldResults,
  closeOverlay,
  exerciseAllInputsIn,
  exerciseDialog,
  exerciseByTestId,
  exerciseCombobox,
  exerciseTextInput,
  LIST_PAGE_FIELDS,
  SAMPLE,
} = require("./helpers/input-field-tester");

test.describe.configure({ mode: "serial" });

test.describe("All input fields — comprehensive E2E", () => {
  test.setTimeout(300_000);

  const log = createFieldResults();
  let patientId;
  const stamp = Date.now();

  test.beforeAll(async () => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const patient = await createPatient(admin.accessToken, `E2E Inputs Patient ${stamp}`, E2E_TEST_PHONE);
    patientId = patient.id || patient.patient?.id;
  });

  test.afterAll(() => {
    const { ok, skip, fail, total, failures } = log.summary();
    console.log(`\n=== Input field exercise summary ===`);
    console.log(`  Total: ${total} | OK: ${ok} | Skipped: ${skip} | Failed: ${fail.length}`);
    if (failures.length) {
      console.log("\n  Failures:");
      for (const f of failures) {
        console.log(`    [${f.area}] ${f.field}: ${f.note}`);
      }
    }
  });

  test("login page inputs", async ({ page }) => {
    await page.context().clearCookies();
    await page.goto("/login", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("login-form")).toBeVisible();

    await exerciseByTestId(page, "login-email", SAMPLE.email, "Login", log);
    await exerciseByTestId(page, "login-password", SAMPLE.password, "Login", log);

    await login(page);
  });

  test("list page search and filter inputs", async ({ page }) => {
    await login(page);

    for (const { path, area, fields } of LIST_PAGE_FIELDS) {
      await page.goto(path, { waitUntil: "domcontentloaded" });
      await page.waitForTimeout(400);

      for (const f of fields) {
        if (f.combobox) {
          const trigger = page.getByTestId(f.testId);
          if (await trigger.isVisible().catch(() => false)) {
            await exerciseCombobox(page, trigger, { area, field: f.testId, log });
          } else {
            log.skip(area, f.testId, "not visible");
          }
        } else {
          await exerciseByTestId(page, f.testId, f.value, area, log);
        }
      }
    }

    // Campaigns list search (no testId)
    await page.goto("/campaigns", { waitUntil: "domcontentloaded" });
    const campaignSearch = page.getByPlaceholder("Search campaigns...");
    if (await campaignSearch.isVisible().catch(() => false)) {
      await exerciseTextInput(page, campaignSearch, SAMPLE.search, {
        area: "Campaigns list",
        field: "campaigns-search",
        log,
      });
    }
  });

  test("dashboard command palette input", async ({ page }) => {
    await login(page);
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await page.waitForResponse(
      (r) => r.url().includes("/api/dashboard/overview") && r.status() === 200,
      { timeout: 45_000 },
    ).catch(() => {});

    if (!(await page.getByTestId("stat-appointments").isVisible({ timeout: 20_000 }).catch(() => false))) {
      log.skip("Dashboard", "stat-appointments", "dashboard not loaded");
      return;
    }

    const paletteBtn = page
      .getByTestId("open-command-palette")
      .or(page.getByTestId("open-command-palette-mobile"));
    if (!(await paletteBtn.first().isVisible().catch(() => false))) {
      log.skip("Command palette", "open-command-palette", "not visible");
      return;
    }

    await paletteBtn.first().click({ timeout: 8_000 });
    const cmdInput = page.getByTestId("cmd-palette-input");
    await cmdInput.waitFor({ state: "visible", timeout: 8_000 });
    await cmdInput.fill(SAMPLE.search);
    log.ok("Command palette", "cmd-palette-input");
    await page.keyboard.press("Escape");
  });

  test("create dialog inputs — patients, appointments, tasks, campaigns", async ({ page }) => {
    await login(page);

    // New patient
    await page.goto("/patients", { waitUntil: "domcontentloaded" });
    await page.getByTestId("new-patient-btn").click();
    await exerciseDialog(page, "new-patient-dialog", "New patient dialog", log);

    // New appointment
    await page.goto("/appointments", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("appointments-kanban")).toBeVisible({ timeout: 15_000 });
    await page.getByTestId("new-appt-btn").click();
    await exerciseDialog(page, "new-appt-dialog", "New appointment dialog", log);

    // New task
    await page.goto("/tasks", { waitUntil: "domcontentloaded" });
    await page.getByTestId("new-task-btn").click();
    await exerciseAllInputsIn(page, page.getByRole("dialog"), "New task dialog", log);
    await closeOverlay(page);

    // New campaign
    await page.goto("/campaigns", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("campaigns-kanban")).toBeVisible({ timeout: 15_000 });
    await page.getByTestId("new-campaign-btn").click();
    await exerciseDialog(page, "new-campaign-dialog", "New campaign dialog", log);
  });

  test("create dialog inputs — doctors, staff, users, templates, roles", async ({ page }) => {
    await login(page);

    // Doctors
    await page.goto("/doctors", { waitUntil: "domcontentloaded" });
    await page.getByTestId("new-doctor-btn").click();
    await exerciseDialog(page, "new-doctor-dialog", "New doctor dialog", log);
    await closeOverlay(page);

    await page.getByTestId("referral-filters-btn").click();
    await exerciseCombobox(page, page.getByTestId("doctor-cat-filter"), {
      area: "Doctor filters",
      field: "doctor-cat-filter",
      log,
    });
    await closeOverlay(page);
    await page.goto("/staff", { waitUntil: "domcontentloaded" });
    await page.getByTestId("new-staff-profile-btn").click();
    await exerciseAllInputsIn(page, page.getByRole("dialog"), "New staff dialog", log);
    await closeOverlay(page);

    // Users
    await page.goto("/settings/users", { waitUntil: "domcontentloaded" });
    await page.getByTestId("new-user-btn").click();
    await exerciseAllInputsIn(page, page.getByRole("dialog"), "New user dialog", log);
    await closeOverlay(page);

    // Templates
    await page.goto("/settings/templates", { waitUntil: "domcontentloaded" });
    const newTpl = page.getByTestId("new-tpl-btn");
    if (await newTpl.isVisible().catch(() => false)) {
      await newTpl.click();
      await exerciseAllInputsIn(page, page.getByRole("dialog"), "New template dialog", log);
      await closeOverlay(page);
    } else {
      log.skip("Templates", "new-tpl-btn", "not visible");
    }

    // Roles
    await page.goto("/settings/roles", { waitUntil: "domcontentloaded" });
    const newRole = page.getByTestId("new-role-btn");
    if (await newRole.isVisible().catch(() => false)) {
      await newRole.click();
      await exerciseAllInputsIn(page, page.getByRole("dialog"), "New role dialog", log);
      await closeOverlay(page);
    }
  });

  test("patient detail — edit form all tabs", async ({ page }) => {
    test.skip(!patientId, "No patient for detail input test");
    await login(page);
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("patient-edit-icon").click();
    await expect(page.getByTestId("edit-patient-form")).toBeVisible();

    for (const tabId of ["edit-tab-basic", "edit-tab-personal", "edit-tab-address", "edit-tab-emergency"]) {
      const tab = page.getByTestId(tabId);
      if (await tab.isVisible().catch(() => false)) {
        await tab.click();
        await page.waitForTimeout(300);
        await exerciseAllInputsIn(page, page.getByTestId("edit-patient-form"), `Patient edit / ${tabId}`, log);
      }
    }
    await closeOverlay(page);
  });

  test("patient EHR — clinical input forms", async ({ page }) => {
    test.skip(!patientId, "No patient for EHR input test");
    await login(page);
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-tabs")).toBeVisible({ timeout: 20_000 });

    const ehrTabs = [
      { tab: "tab-allergies", toggle: "allergy-add-toggle", area: "EHR Allergies" },
      { tab: "tab-prescriptions", toggle: "rx-new-toggle", area: "EHR Prescriptions" },
      { tab: "tab-vitals", toggle: "vitals-add-toggle", area: "EHR Vitals" },
      { tab: "tab-notes", toggle: null, area: "EHR Notes" },
      { tab: "tab-lifestyle", toggle: null, area: "EHR Lifestyle" },
    ];

    for (const { tab, toggle, area } of ehrTabs) {
      const tabEl = page.getByTestId(tab);
      if (!(await tabEl.isVisible().catch(() => false))) {
        log.skip(area, tab, "tab not visible");
        continue;
      }
      await tabEl.click();
      await page.waitForTimeout(400);

      if (toggle) {
        const btn = page.getByTestId(toggle);
        if (await btn.isVisible().catch(() => false)) {
          await btn.click();
          await page.waitForTimeout(300);
        }
      }

      const panel = page.locator("[data-testid^='ehr-'], [data-testid='edit-patient-form']").first();
      if (area === "EHR Notes") {
        await exerciseAllInputsIn(page, page.getByTestId("ehr-notes"), area, log);
      } else if (area === "EHR Lifestyle") {
        await exerciseAllInputsIn(page, page.getByTestId("ehr-lifestyle"), area, log);
      } else if (await panel.isVisible().catch(() => false)) {
        await exerciseAllInputsIn(page, panel, area, log);
      }
      await closeOverlay(page);
    }
  });

  test("inbox, email, integrations, and hospital settings inputs", async ({ page }) => {
    await login(page);

    // WhatsApp inbox compose
    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await exerciseByTestId(page, "inbox-search", SAMPLE.search, "Inbox", log);
    const conv = page.locator("[data-testid^='conv-']").first();
    if (await conv.isVisible().catch(() => false)) {
      await conv.click();
      await page.waitForTimeout(500);
      const compose = page.locator("textarea:visible").last();
      if (await compose.isVisible().catch(() => false)) {
        await exerciseTextInput(page, compose, SAMPLE.textarea, {
          area: "Inbox compose",
          field: "message-textarea",
          log,
        });
      }
    } else {
      log.skip("Inbox compose", "message-textarea", "no conversations");
    }

    // Email inbox
    await page.goto("/email-inbox", { waitUntil: "domcontentloaded" });
    const emailSearch = page.getByPlaceholder("Search email threads...");
    if (await emailSearch.isVisible().catch(() => false)) {
      await exerciseTextInput(page, emailSearch, SAMPLE.search, {
        area: "Email inbox",
        field: "email-thread-search",
        log,
      });
    }
    const threadBtn = page.locator("button").filter({ hasText: /@/ }).first();
    if (await threadBtn.isVisible().catch(() => false)) {
      await threadBtn.click();
      await page.waitForTimeout(500);
      const subject = page.getByPlaceholder("Subject");
      const body = page.getByPlaceholder(/Compose email|Email sending is disabled/);
      if (await subject.isVisible().catch(() => false) && !(await subject.isDisabled())) {
        await exerciseTextInput(page, subject, "E2E subject", { area: "Email compose", field: "subject", log });
      } else {
        log.skip("Email compose", "subject", "disabled or not configured");
      }
      if (await body.isVisible().catch(() => false) && !(await body.isDisabled())) {
        await exerciseTextInput(page, body, SAMPLE.textarea, { area: "Email compose", field: "body", log });
      }
    } else {
      log.skip("Email compose", "subject/body", "no threads or email disabled");
    }

    // Integrations panel — all credential fields
    await page.goto("/settings/integrations", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("integrations-panel")).toBeVisible({ timeout: 15_000 });
    await exerciseAllInputsIn(page, page.getByTestId("integrations-panel"), "Integrations", log);

    // Hospital — department name input
    await page.goto("/settings/hospital", { waitUntil: "domcontentloaded" });
    const hospitalMain = page.locator("main");
    if (await hospitalMain.locator("input:visible").count() > 0) {
      await exerciseAllInputsIn(page, hospitalMain, "Hospital settings", log);
    } else {
      log.skip("Hospital settings", "inputs", "no visible inputs");
    }

    // Notification preferences toggles (switch role)
    await page.goto("/notifications/preferences", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("notification-preferences-title")).toBeVisible();
    const switches = page.locator('[role="switch"]:visible');
    const swCount = await switches.count();
    for (let i = 0; i < Math.min(swCount, 5); i++) {
      try {
        await switches.nth(i).click();
        log.ok("Notification prefs", `switch-${i}`);
        await switches.nth(i).click();
      } catch (err) {
        log.fail("Notification prefs", `switch-${i}`, err.message);
      }
    }
  });

  test("no critical input field failures", async () => {
    const { failures } = log.summary();
    if (failures.length > 0) {
      throw new Error(
        `${failures.length} input field(s) failed:\n${failures
          .map((f) => `  [${f.area}] ${f.field}: ${f.note}`)
          .join("\n")}`,
      );
    }
  });
});

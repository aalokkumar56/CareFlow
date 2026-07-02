const { test, expect } = require("@playwright/test");
const { login, selectRadixOption } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const {
  ALL_ROLES_MATRIX,
  PATIENT_STATUS_FILTERS,
  PATIENT_DEPT_FILTERS,
  PATIENT_SOURCE_FILTERS,
  SEARCH_QUERIES,
  PATIENT_TABS,
  PATIENT_EDIT_TABS,
  routeAllowed,
} = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../src/lib/permissions");
const { apiLogin, createPatient, patchPatient } = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");

test.describe.configure({ mode: "serial" });

test.describe("Patients matrix — filters × search × tabs × roles", () => {
  let roles = {};
  let patientId;
  const filterName = `Matrix Patient ${Date.now()}`;

  test.beforeAll(async () => {
    roles = await getRoleSessions();
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const created = await createPatient(admin.accessToken, filterName, E2E_TEST_PHONE);
    patientId = created.id || created.patient?.id;
    if (patientId) {
      await patchPatient(admin.accessToken, patientId, {
        status: "visited",
        department: "Cardiology",
        inquiry_source: "manual",
        tags: ["e2e-matrix"],
      });
    }
  });

  const patientListRoute = { permission: PERMISSIONS.PatientView };

  for (const role of ALL_ROLES_MATRIX) {
    for (const status of PATIENT_STATUS_FILTERS) {
      test(`${role}: status filter "${status}" triggers API`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(patientListRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        const listPromise = page.waitForResponse((r) => r.url().includes("/api/patients?") && r.ok());
        await page.goto("/patients", { waitUntil: "domcontentloaded" });
        await listPromise;
        await expect(page.getByTestId("patients-search")).toBeVisible();

        if (status === "All Statuses") return;

        const filterPromise = page.waitForResponse(
          (r) => r.url().includes("/api/patients?") && r.url().includes("status=") && r.ok(),
          { timeout: 15_000 },
        );
        await selectRadixOption(page, "filter-status", status);
        await filterPromise;
        await expect(page.getByTestId("patients-filter-count")).toContainText(/patient/i);
      });
    }

    for (const dept of PATIENT_DEPT_FILTERS) {
      test(`${role}: department filter "${dept}"`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(patientListRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/patients", { waitUntil: "networkidle" });
        if (dept === "All Departments") return;

        const filterPromise = page.waitForResponse(
          (r) => r.url().includes(`department=${encodeURIComponent(dept)}`) && r.ok(),
          { timeout: 15_000 },
        );
        await selectRadixOption(page, "filter-dept", dept);
        await filterPromise;
      });
    }

    for (const source of PATIENT_SOURCE_FILTERS) {
      test(`${role}: source filter "${source}"`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(patientListRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/patients", { waitUntil: "networkidle" });
        if (source === "All Sources") return;

        const filterPromise = page.waitForResponse(
          (r) => r.url().includes("/api/patients?") && r.ok(),
          { timeout: 15_000 },
        );
        await selectRadixOption(page, "filter-source", source);
        await filterPromise;
      });
    }

    for (const query of SEARCH_QUERIES) {
      test(`${role}: search "${query || "(empty)"}"`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(patientListRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/patients", { waitUntil: "networkidle" });

        const searchPromise = page.waitForResponse(
          (r) => r.url().includes("/api/patients?") && r.ok(),
          { timeout: 15_000 },
        );
        await page.getByTestId("patients-search").fill(query);
        await searchPromise;
      });
    }
  }

  for (const role of ALL_ROLES_MATRIX) {
    for (const tabId of PATIENT_TABS) {
      test(`${role}: patient detail tab ${tabId}`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(patientListRoute, session.permissions) || !patientId);

        await login(page, { email: session.email, password: session.password });
        await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
        await expect(page.getByTestId("patient-tabs")).toBeVisible();

        const tab = page.getByTestId(tabId);
        if (!(await tab.isVisible().catch(() => false))) return;

        await tab.click();
        await page.waitForTimeout(300);
        await expect(tab).toHaveAttribute("data-state", "active");
      });
    }

    for (const tabId of PATIENT_EDIT_TABS) {
      test(`${role}: patient edit tab ${tabId}`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(patientListRoute, session.permissions) || !patientId);
        test.skip(!session.permissions.includes(PERMISSIONS.PatientEdit), `${role} cannot edit`);

        await login(page, { email: session.email, password: session.password });
        await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
        await page.getByTestId("patient-edit-icon").click();
        await expect(page.getByTestId("edit-patient-form")).toBeVisible();

        const tab = page.getByTestId(tabId);
        await tab.click();
        await expect(tab).toHaveAttribute("data-state", "active");
      });
    }
  }
});

const { test } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin, apiRequest, createPatient } = require("./helpers/api");
const { APPSHELL_PAGES, DESIGN_AUDIT_PAGES } = require("./helpers/routes");
const {
  auditPageUI,
  auditPageHeading,
  auditBorderRadiusMix,
  auditAppshellFeatures,
  auditPrimaryButtonColor,
} = require("./helpers/ui-audit");
const { reportBug } = require("./helpers/bug-log");

test.describe.configure({ mode: "serial" });

test.describe("Design consistency audit", () => {
  let patientDetailPath;
  let doctorDetailPath;

  test.beforeAll(async () => {
    const stamp = Date.now();
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const patient = await createPatient(admin.accessToken, `Design Audit ${stamp}`, `91${String(stamp).slice(-8)}`);
    const id = patient.id || patient.patient?.id;
    if (id) patientDetailPath = `/patients/${id}`;

    try {
      const doctors = await apiRequest(admin.accessToken, "GET", "/doctors");
      const list = Array.isArray(doctors) ? doctors : doctors?.items || doctors?.data || [];
      if (list.length > 0) doctorDetailPath = `/doctors/${list[0].id}`;
    } catch { /* optional */ }
  });

  test("page headings present on all major pages", async ({ page }) => {
    await login(page);
    const pages = [...DESIGN_AUDIT_PAGES];
    if (patientDetailPath) pages.push(patientDetailPath);

    for (const path of pages) {
      await page.goto(path, { waitUntil: "networkidle" });
      await page.waitForTimeout(350);

      const heading = await auditPageHeading(page);
      const isPatientDetail = path.includes("/patients/") && path !== "/patients";
      const isDashboard = path === "/";

      if (isPatientDetail) {
        if (!heading.hasBreadcrumb) {
          reportBug({
            severity: "Medium",
            page: path,
            title: "Patient detail missing breadcrumb",
            expected: "page-breadcrumb visible",
          });
        }
        if (heading.hasPageTitle) {
          reportBug({
            severity: "Medium",
            page: path,
            title: "Patient detail has duplicate page-title in AppShell",
          });
        }
      } else if (!heading.hasPageTitle && !heading.hasBreadcrumb && !isDashboard) {
        reportBug({
          severity: "Medium",
          page: path,
          title: "Missing page-title or breadcrumb",
        });
      }
    }
  });

  test("primary button colors consistent across list pages", async ({ page }) => {
    await login(page);
    const paths = ["/patients", "/appointments", "/tasks", "/campaigns", "/staff", "/settings/users"];
    const colors = new Map();

    for (const path of paths) {
      await page.goto(path, { waitUntil: "networkidle" });
      const bg = await auditPrimaryButtonColor(page);
      if (bg) colors.set(path, bg);
    }

    const unique = new Set(colors.values());
    if (unique.size > 2) {
      reportBug({
        severity: "Medium",
        page: "multiple",
        title: "Inconsistent primary button background colors",
        actual: [...colors.entries()].map(([p, c]) => `${p}: ${c}`).join("; "),
      });
    }
  });

  test("border radius mixing flagged on key pages", async ({ page }) => {
    await login(page);
    const checkPaths = ["/patients", "/appointments", "/campaigns", "/settings/templates"];

    for (const path of checkPaths) {
      await page.goto(path, { waitUntil: "networkidle" });
      const radii = await auditBorderRadiusMix(page);
      const hasSm = radii.includes("rounded-sm");
      const hasXl = radii.includes("rounded-xl");
      const has2xl = radii.includes("rounded-2xl");

      if ((hasSm && hasXl) || (hasSm && has2xl) || (hasXl && has2xl)) {
        reportBug({
          severity: "Low",
          page: path,
          title: "Mixed border radius classes on same page",
          actual: radii.join(", "),
          expected: "Consistent rounded-sm per design_guidelines.json",
        });
      }
    }
  });

  test("AppShell notification bell inconsistency across list pages", async ({ page }) => {
    await login(page);
    const bellVisible = [];
    const bellHidden = [];

    for (const { path, label, hideNotifications } of APPSHELL_PAGES) {
      await page.goto(path, { waitUntil: "networkidle" });
      const shell = await auditAppshellFeatures(page, {
        expectCommandPalette: !APPSHELL_PAGES.find((p) => p.path === path)?.hideHeaderSearch,
        expectNotificationBell: !hideNotifications,
      });

      if (shell.bell) bellVisible.push(label);
      else bellHidden.push(label);

      shell.issues.forEach((issue) => {
        reportBug({
          severity: "Low",
          page: path,
          title: `AppShell chrome mismatch: ${issue}`,
          expected: hideNotifications ? "bell hidden" : "bell visible",
          actual: shell.bell ? "bell visible" : "bell hidden",
        });
      });
    }

    if (bellVisible.length > 0 && bellHidden.length > 0) {
      reportBug({
        severity: "Medium",
        page: "AppShell",
        title: "Inconsistent notification bell visibility across list pages",
        actual: `Visible: ${bellVisible.join(", ")} | Hidden: ${bellHidden.join(", ")}`,
        expected: "Bell visible on all standard AppShell pages",
      });
    }
  });

  test("command palette hidden on pages with hideHeaderSearch", async ({ page }) => {
    await login(page);
    const pagesWithHiddenPalette = APPSHELL_PAGES.filter((p) => p.hideHeaderSearch);

    for (const { path, label } of pagesWithHiddenPalette) {
      await page.goto(path, { waitUntil: "networkidle" });
      const palette = await page.getByTestId("open-command-palette").isVisible().catch(() => false);
      if (palette) {
        reportBug({
          severity: "Low",
          page: path,
          title: `Command palette visible on ${label} despite hideHeaderSearch`,
        });
      }
    }
  });

  test("UI audit issues on each design page", async ({ page }) => {
    await login(page);

    for (const path of DESIGN_AUDIT_PAGES) {
      await page.goto(path, { waitUntil: "networkidle" });
      await page.waitForTimeout(300);
      const ui = await auditPageUI(page, path);
      ui.issues.forEach((issue) => {
        const severity = issue.includes("button style variants") ? "Low" : "Medium";
        reportBug({ severity, page: path, title: issue.split(":")[0], actual: issue });
      });
    }
  });

  test("tab styling sample: patient vs doctor vs settings notifications", async ({ page }) => {
    await login(page);

    const tabSamples = [];

    if (patientDetailPath) {
      await page.goto(patientDetailPath, { waitUntil: "networkidle" });
      const patientTab = page.getByTestId("tab-details");
      if (await patientTab.isVisible().catch(() => false)) {
        tabSamples.push({
          page: "patient detail",
          style: await patientTab.evaluate((el) => {
            const s = window.getComputedStyle(el);
            return { borderRadius: s.borderRadius, fontSize: s.fontSize, padding: s.padding };
          }),
        });
      }
    }

    if (doctorDetailPath) {
      await page.goto(doctorDetailPath, { waitUntil: "networkidle" });
      const docTab = page.getByRole("tab").first();
      if (await docTab.isVisible().catch(() => false)) {
        tabSamples.push({
          page: "doctor detail",
          style: await docTab.evaluate((el) => {
            const s = window.getComputedStyle(el);
            return { borderRadius: s.borderRadius, fontSize: s.fontSize, padding: s.padding };
          }),
        });
      }
    }

    await page.goto("/settings/notifications", { waitUntil: "networkidle" });
    const notifTab = page.getByRole("button", { name: "My preferences", exact: true });
    if (await notifTab.isVisible().catch(() => false)) {
      tabSamples.push({
        page: "settings notifications",
        style: await notifTab.evaluate((el) => {
          const s = window.getComputedStyle(el);
          return { borderRadius: s.borderRadius, fontSize: s.fontSize, padding: s.padding };
        }),
      });
    }

    if (tabSamples.length >= 2) {
      const radii = new Set(tabSamples.map((t) => t.style.borderRadius));
      const fonts = new Set(tabSamples.map((t) => t.style.fontSize));
      if (radii.size > 1 || fonts.size > 1) {
        reportBug({
          severity: "Low",
          page: "tabs",
          title: "Tab styling drift across PatientDetail / DoctorDetail / Settings",
          actual: JSON.stringify(tabSamples),
        });
      }
    }
  });
});

const path = require("path");
const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin, apiRequest, createPatient } = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");
const { DESIGN_AUDIT_PAGES } = require("./helpers/routes");
const { reportBug } = require("./helpers/bug-log");
const {
  sampleSurfaceStyles,
  isGlassLikeSurface,
  auditKanbanColumnGlass,
  auditKanbanCardGlass,
  auditGlassmorphism,
} = require("./helpers/ui-audit");

const RUN_TS = new Date()
  .toISOString()
  .slice(0, 16)
  .replace("T", "_")
  .replace(":", "-");
const SCREENSHOT_DIR = path.join(
  "D:\\Projects\\Sarvik\\Care-Flow\\screenshots",
  RUN_TS,
  "glassmorphism-audit",
);

/** @param {import('@playwright/test').Page} page @param {string} name */
async function capturePage(_page, name) {
  // Screenshots disabled — keep a path string for bug-log references only.
  return path.join(SCREENSHOT_DIR, `${name}.png`);
}

test.describe.configure({ mode: "serial" });

test.describe("Glassmorphism consistency audit", () => {
  let patientDetailPath;
  let doctorDetailPath;

  test.beforeAll(async () => {
    const stamp = Date.now();
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const patient = await createPatient(
      admin.accessToken,
      `Glass Audit ${stamp}`,
      E2E_TEST_PHONE,
    );
    const id = patient.id || patient.patient?.id;
    if (id) patientDetailPath = `/patients/${id}`;

    try {
      const doctors = await apiRequest(admin.accessToken, "GET", "/doctors");
      const list = Array.isArray(doctors) ? doctors : doctors?.items || doctors?.data || [];
      if (list.length > 0) doctorDetailPath = `/doctors/${list[0].id}`;
    } catch {
      /* optional */
    }
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("sidebar glass reference matches design tokens", async ({ page }) => {
    await page.goto("/", { waitUntil: "networkidle" });
    const sidebar = await sampleSurfaceStyles(page, ".glass-sidebar");
    expect(sidebar).toBeTruthy();
    expect(isGlassLikeSurface(sidebar)).toBe(true);
    await capturePage(page, "00-dashboard-reference");
  });

  const kanbanPages = [
    { path: "/appointments", boardTestId: "appointments-kanban", label: "Appointments" },
    { path: "/tasks", boardTestId: "followups-kanban", label: "Follow-ups" },
    { path: "/campaigns", boardTestId: "campaigns-kanban", label: "Campaigns" },
  ];

  for (const { path: routePath, boardTestId, label } of kanbanPages) {
    test(`${label} kanban columns and cards use glass surfaces`, async ({ page }) => {
      await page.goto(routePath, { waitUntil: "networkidle" });
      await expect(page.getByTestId(boardTestId)).toBeVisible({ timeout: 20_000 });

      const screenshot = await capturePage(page, routePath.replace(/\//g, "").replace(/^$/, "home") || "page");

      const colAudit = await auditKanbanColumnGlass(page, boardTestId);
      for (const issue of colAudit.issues) {
        reportBug({
          severity: "High",
          category: "Glassmorphism",
          page: routePath,
          title: `${label} kanban column ${issue.columnIndex + 1} solid background`,
          expected: "Semi-transparent bg with backdrop-blur (glass-card pattern)",
          actual: `${issue.backgroundColor}; classes: ${issue.className}`,
          screenshot,
        });
      }

      const cardAudit = await auditKanbanCardGlass(page, boardTestId);
      for (const issue of cardAudit.issues) {
        reportBug({
          severity: "Medium",
          category: "Glassmorphism",
          page: routePath,
          title: `${label} kanban card solid background`,
          expected: "Semi-transparent card (bg-white/55 or glass-card)",
          actual: `${issue.backgroundColor}; classes: ${issue.className}`,
          screenshot,
        });
      }

      const outerGlass = await sampleSurfaceStyles(page, `[data-testid="${boardTestId}"]`);
      if (outerGlass && !isGlassLikeSurface(outerGlass)) {
        reportBug({
          severity: "High",
          category: "Glassmorphism",
          page: routePath,
          title: `${label} kanban outer GlassCard is not glass`,
          actual: `${outerGlass.backgroundColor}; classes: ${outerGlass.className}`,
          screenshot,
        });
      }
    });
  }

  const listPages = [
    { path: "/patients", surfaces: [{ selector: ".glass-card", label: "main GlassCard" }, { selector: "thead", label: "table header" }] },
    { path: "/staff", surfaces: [{ selector: ".glass-card", label: "main GlassCard" }] },
    { path: "/doctors", surfaces: [{ selector: ".glass-card", label: "main GlassCard" }] },
  ];

  for (const { path: routePath, surfaces } of listPages) {
    test(`list page glass surfaces on ${routePath}`, async ({ page }) => {
      await page.goto(routePath, { waitUntil: "networkidle" });
      const slug = routePath.replace(/\//g, "-").slice(1);
      const screenshot = await capturePage(page, slug);

      for (const { selector, label } of surfaces) {
        const styles = await sampleSurfaceStyles(page, selector);
        if (!styles) continue;
        if (!isGlassLikeSurface(styles)) {
          const severity = label.includes("header") ? "Medium" : "Low";
          reportBug({
            severity,
            category: "Glassmorphism",
            page: routePath,
            title: `${routePath} ${label} uses solid background`,
            expected: "Glass surface matching sidebar (rgba + backdrop-blur)",
            actual: `${styles.backgroundColor}; classes: ${styles.className}`,
            screenshot,
          });
        }
      }
    });
  }

  test("patient detail clinical panels vs glass tabs", async ({ page }) => {
    if (!patientDetailPath) test.skip();
    await page.goto(patientDetailPath, { waitUntil: "networkidle" });
    const screenshot = await capturePage(page, "patient-detail");

    const tabBar = await sampleSurfaceStyles(page, "[data-testid='patient-tabs'] .glass-card, .glass-card");
    if (tabBar && !isGlassLikeSurface(tabBar)) {
      reportBug({
        severity: "Medium",
        category: "Glassmorphism",
        page: patientDetailPath,
        title: "Patient detail tab bar not glass",
        actual: `${tabBar.backgroundColor}; ${tabBar.className}`,
        screenshot,
        filePath: "src/pages/PatientDetail.jsx",
      });
    }

    const detailsTab = page.getByTestId("tab-details");
    if (await detailsTab.isVisible().catch(() => false)) {
      await detailsTab.click();
      await page.waitForTimeout(300);
    }

    const detailsPanel = await sampleSurfaceStyles(page, "[data-testid='patient-details-panel']");
    if (detailsPanel && !isGlassLikeSurface(detailsPanel)) {
      reportBug({
        severity: "Medium",
        category: "Glassmorphism",
        page: patientDetailPath,
        title: "Patient details panel uses solid white (bg-white)",
        expected: "glass-card or bg-white/xx with backdrop-blur",
        actual: `${detailsPanel.backgroundColor}; classes: ${detailsPanel.className}`,
        screenshot,
        component: "patient-details-panel",
        cssClass: "bg-white",
        filePath: "src/pages/PatientDetail.jsx",
      });
    }

    for (const tabId of ["tab-allergies", "tab-prescriptions", "tab-vitals", "tab-notes", "tab-history", "tab-lifestyle"]) {
      const tab = page.getByTestId(tabId);
      if ((await tab.count()) === 0) continue;
      await tab.click();
      await page.waitForTimeout(300);
      const opaquePanel = await page.evaluate(() => {
        const panels = [...document.querySelectorAll("main [class*='bg-white']")].filter((el) => {
          const cn = el.className.toString();
          if (/bg-white\/\d/.test(cn)) return false;
          const s = window.getComputedStyle(el);
          const rect = el.getBoundingClientRect();
          const m = s.backgroundColor.match(/rgba?\([^)]+\)/);
          let alpha = 1;
          if (m) {
            const parts = m[0].match(/[\d.]+/g) || [];
            if (parts.length === 4) alpha = parseFloat(parts[3]);
          }
          return rect.width >= 200 && rect.height >= 80 && alpha >= 0.92;
        });
        return panels.slice(0, 3).map((el) => ({
          testId: el.getAttribute("data-testid"),
          className: el.className.toString().slice(0, 100),
          bg: window.getComputedStyle(el).backgroundColor,
        }));
      });
      for (const p of opaquePanel) {
        reportBug({
          severity: "Medium",
          category: "Glassmorphism",
          page: `${patientDetailPath} (${tabId})`,
          title: `EHR panel solid white background`,
          expected: "GlassCard / glass-card pattern",
          actual: `${p.bg}; ${p.className}`,
          component: p.testId || tabId,
          cssClass: "bg-white",
          filePath: `src/pages/ehr/*.jsx or PatientDetail.jsx`,
          screenshot,
        });
      }
    }
  });

  test("settings notifications preferences solid panels", async ({ page }) => {
    await page.goto("/settings/notifications", { waitUntil: "networkidle" });
    const screenshot = await capturePage(page, "settings-notifications");

    const tabList = await sampleSurfaceStyles(page, '[role="tablist"], .TabsList, [class*="TabsList"]');
    if (tabList && !isGlassLikeSurface(tabList)) {
      reportBug({
        severity: "Medium",
        category: "Glassmorphism",
        page: "/settings/notifications",
        title: "Notification settings tab list solid background",
        actual: `${tabList.backgroundColor}; ${tabList.className}`,
        screenshot,
      });
    }

    const sectionHeader = await sampleSurfaceStyles(page, ".bg-\\[\\#FAFAFA\\], [class*='FAFAFA']");
    if (sectionHeader && !isGlassLikeSurface(sectionHeader)) {
      reportBug({
        severity: "Low",
        category: "Glassmorphism",
        page: "/settings/notifications",
        title: "Notification preference section header solid #FAFAFA",
        actual: `${sectionHeader.backgroundColor}; ${sectionHeader.className}`,
        screenshot,
      });
    }
  });

  test("full route crawl — opaque panels on every design page", async ({ page }) => {
    const extra = ["/login", "/notifications/preferences"];
    const pages = [...DESIGN_AUDIT_PAGES, ...extra];
    if (patientDetailPath) pages.push(patientDetailPath);
    if (doctorDetailPath) pages.push(doctorDetailPath);

    for (const routePath of pages) {
      if (routePath === "/login") {
        await page.evaluate(() => {
          localStorage.removeItem("cureflow_token");
          localStorage.removeItem("cureflow_user");
        });
        await page.goto(routePath, { waitUntil: "networkidle" });
      } else {
        await login(page);
        await page.goto(routePath, { waitUntil: "networkidle" });
      }
      await page.waitForTimeout(350);

      const slug = routePath.replace(/\//g, "-").replace(/^-/, "") || "dashboard";
      const screenshot = await capturePage(page, `crawl-${slug}`);

      const audit = await auditGlassmorphism(page, routePath);
      for (const issue of audit.issues) {
        const severity =
          issue.issue === "solid-card-instead-of-glass" || issue.issue === "opaque-bg-white-panel"
            ? "Medium"
            : issue.issue.includes("glass-class")
              ? "High"
              : "Medium";

        reportBug({
          severity,
          category: "UI/Glassmorphism",
          page: routePath,
          title: `${issue.issue}: ${issue.component}`,
          expected: "Glassmorphism — rgba alpha < 0.88 + backdrop-blur (see glass-card in index.css)",
          actual: `bg=${issue.background}; alpha=${issue.alpha}; backdrop=${issue.backdropFilter}; ${issue.width}x${issue.height}px`,
          component: issue.component,
          cssClass: issue.cssClass,
          filePath: issue.fileHint,
          screenshot,
        });
      }
    }
  });
});

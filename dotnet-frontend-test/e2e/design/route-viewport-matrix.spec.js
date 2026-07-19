const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, createPatient } = require("../helpers/api");
const { E2E_TEST_PHONE } = require("../helpers/constants");
const {
  VIEWPORTS,
  PRIMARY_TEXT_RGB,
  SAMPLE_MODE,
  getDesignRoutes,
  routeSlug,
  gotoDesignRoute,
} = require("../helpers/design-matrix");
const {
  auditPageUI,
  auditPageHeading,
  setupConsoleCapture,
  sampleSurfaceStyles,
  isGlassLikeSurface,
} = require("../helpers/ui-audit");
const { reportBug } = require("../helpers/bug-log");

test.describe.configure({ mode: "serial" });

test.describe("Design matrix — route × viewport × audit dimension", () => {
  /** @type {string | undefined} */
  let patientDetailPath;

  test.beforeAll(async () => {
    if (SAMPLE_MODE) return;
    const stamp = Date.now();
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const patient = await createPatient(admin.accessToken, `Design Matrix ${stamp}`, E2E_TEST_PHONE);
    const id = patient.id || patient.patient?.id;
    if (id) patientDetailPath = `/patients/${id}`;
  });

  for (const viewport of VIEWPORTS) {
    for (const routePath of getDesignRoutes({})) {
      const slug = `${routeSlug(routePath)}@${viewport.id}`;

      test(`${slug} — layout-no-overflow`, async ({ page }) => {
        if (routePath !== "/login") await login(page);
        await gotoDesignRoute(page, routePath, viewport);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2);
        expect(overflow, `${routePath} horizontal overflow at ${viewport.id}`).toBe(false);
      });

      test(`${slug} — page-chrome`, async ({ page }) => {
        if (routePath === "/login") {
          await gotoDesignRoute(page, routePath, viewport);
          await expect(page.getByTestId("login-email")).toBeVisible();
          return;
        }
        await login(page);
        await gotoDesignRoute(page, routePath, viewport);
        const heading = await auditPageHeading(page);
        const isDashboard = routePath === "/";
        const isDetail = /\/(patients|doctors)\/[^/]+/.test(routePath);
        if (!isDashboard && !isDetail && !heading.hasPageTitle && !heading.hasBreadcrumb) {
          reportBug({ severity: "Medium", page: routePath, title: `Missing page chrome at ${viewport.id}` });
        }
      });

      test(`${slug} — title-font`, async ({ page }) => {
        if (routePath === "/login") {
          await gotoDesignRoute(page, routePath, viewport);
          return;
        }
        await login(page);
        await gotoDesignRoute(page, routePath, viewport);
        const title = page.locator("[data-testid='page-title'], [data-testid='page-breadcrumb']").first();
        if ((await title.count()) === 0) return;
        const font = await title.evaluate((el) => window.getComputedStyle(el).fontFamily);
        expect(font.toLowerCase()).toMatch(/outfit|heading|inter/);
      });

      test(`${slug} — title-color`, async ({ page }) => {
        if (routePath === "/login") {
          await gotoDesignRoute(page, routePath, viewport);
          return;
        }
        await login(page);
        await gotoDesignRoute(page, routePath, viewport);
        const title = page.locator("[data-testid='page-title']").first();
        if ((await title.count()) === 0) return;
        const color = await title.evaluate((el) => window.getComputedStyle(el).color);
        if (color !== PRIMARY_TEXT_RGB) {
          reportBug({
            severity: "Low",
            page: routePath,
            title: `Title color drift at ${viewport.id}`,
            expected: PRIMARY_TEXT_RGB,
            actual: color,
          });
        }
      });

      test(`${slug} — sidebar-glass`, async ({ page }) => {
        if (routePath === "/login") {
          test.skip();
          return;
        }
        await login(page);
        await gotoDesignRoute(page, routePath, viewport);
        const sidebar = await sampleSurfaceStyles(page, ".glass-sidebar");
        if (viewport.width >= 768 && sidebar) {
          expect(isGlassLikeSurface(sidebar)).toBe(true);
        }
      });

      test(`${slug} — no-broken-images`, async ({ page }) => {
        if (routePath !== "/login") await login(page);
        await gotoDesignRoute(page, routePath, viewport);
        const broken = await page.evaluate(() =>
          [...document.querySelectorAll("img")].filter((img) => img.complete && img.naturalWidth === 0).length,
        );
        expect(broken).toBe(0);
      });

      test(`${slug} — button-variants`, async ({ page }) => {
        if (routePath === "/login") {
          await gotoDesignRoute(page, routePath, viewport);
          return;
        }
        await login(page);
        await gotoDesignRoute(page, routePath, viewport);
        const ui = await auditPageUI(page, routePath);
        const variantIssue = ui.issues.find((i) => i.includes("button style variants"));
        if (variantIssue) {
          reportBug({ severity: "Low", page: routePath, title: variantIssue, actual: variantIssue });
        }
      });

      test(`${slug} — h1-min-size`, async ({ page }) => {
        if (routePath !== "/login") await login(page);
        await gotoDesignRoute(page, routePath, viewport);
        const smallH1 = await page.evaluate(() =>
          [...document.querySelectorAll("h1")].some((el) => {
            const s = window.getComputedStyle(el);
            if (s.display === "none" || s.visibility === "hidden" || el.offsetParent === null) return false;
            return parseFloat(s.fontSize) < 16;
          }),
        );
        if (smallH1) {
          reportBug({
            severity: "Medium",
            page: routePath,
            title: `Visible h1 below 16px at ${viewport.id}`,
          });
        }
      });

      test(`${slug} — console-clean`, async ({ page }) => {
        if (routePath !== "/login") await login(page);
        const capture = setupConsoleCapture(page);
        await gotoDesignRoute(page, routePath, viewport);
        const filtered = capture.getFilteredErrors();
        capture.detach();
        if (filtered.length > 0) {
          reportBug({ severity: "Low", page: routePath, title: `Console errors at ${viewport.id}`, actual: filtered.slice(0, 3).join("; ") });
        }
        expect(filtered.length).toBeLessThanOrEqual(2);
      });

      test(`${slug} — main-surface`, async ({ page }) => {
        if (routePath === "/login") {
          test.skip();
          return;
        }
        await login(page);
        await gotoDesignRoute(page, routePath, viewport);
        const mainCard = await sampleSurfaceStyles(page, "main .glass-card, [data-testid='page-title']");
        if (mainCard && routePath !== "/missed-revenue") {
          expect(isGlassLikeSurface(mainCard) || mainCard.className.includes("font-heading")).toBeTruthy();
        }
      });
    }
  }
});

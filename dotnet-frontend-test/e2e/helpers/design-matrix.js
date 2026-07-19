const path = require("path");
const { DESIGN_AUDIT_PAGES, NAV_ITEMS, PATIENT_TABS } = require("./routes");

/** Mobile, tablet, desktop breakpoints for responsive design matrix. */
const VIEWPORTS = [
  { id: "mobile", width: 375, height: 812 },
  { id: "tablet", width: 768, height: 1024 },
  { id: "desktop", width: 1280, height: 800 },
];

const PRIMARY_TEXT_RGB = "rgb(2, 44, 34)";
const SAMPLE_MODE = process.env.DESIGN_SAMPLE === "1";

/** Static design routes; dynamic detail routes appended at runtime via resolveDynamicRoutes(). */
function getStaticDesignRoutes() {
  const base = [...DESIGN_AUDIT_PAGES, "/login"];
  return SAMPLE_MODE ? base.slice(0, 4) : base;
}

/**
 * @param {{ patientDetailPath?: string, doctorDetailPath?: string }} dynamic
 */
function getDesignRoutes(dynamic = {}) {
  const routes = getStaticDesignRoutes();
  if (!SAMPLE_MODE) {
    if (dynamic.patientDetailPath) routes.push(dynamic.patientDetailPath);
    if (dynamic.doctorDetailPath) routes.push(dynamic.doctorDetailPath);
  }
  return [...new Set(routes)];
}

/** @param {string} routePath */
function routeSlug(routePath) {
  return routePath.replace(/\//g, "-").replace(/^-/, "") || "dashboard";
}

/** @param {{ id: string }} viewport */
function viewportSlug(viewport) {
  return viewport.id;
}

/**
 * @param {import('@playwright/test').Page} page
 * @param {string} routePath
 * @param {{ id: string, width: number, height: number }} viewport
 */
async function gotoDesignRoute(page, routePath, viewport) {
  await page.setViewportSize({ width: viewport.width, height: viewport.height });
  if (routePath === "/login") {
    await page.evaluate(() => {
      localStorage.removeItem("cureflow_token");
      localStorage.removeItem("cureflow_user");
    });
  }
  await page.goto(routePath, { waitUntil: "domcontentloaded" });
  await page.waitForTimeout(viewport.width < 768 ? 450 : 300);
}

/**
 * @param {import('@playwright/test').Page} page
 * @param {string} subdir
 * @param {string} name
 */
async function captureDesignScreenshot(_page, subdir, name) {
  // Screenshots disabled — tests should assert UI behavior, not capture images.
  return path.join("screenshots-disabled", subdir, `${name}.png`);
}

/** @param {import('@playwright/test').Page} page */
async function auditHeadingHierarchy(page) {
  return page.evaluate(() => {
    const issues = [];
    const h1 = document.querySelectorAll("h1").length;
    const h2 = document.querySelectorAll("h2").length;
    const h3 = document.querySelectorAll("h3").length;
    if (h1 > 2) issues.push(`multiple-h1:${h1}`);
    const headings = [...document.querySelectorAll("h1, h2, h3, h4, h5, h6")];
    let lastLevel = 0;
    for (const el of headings.slice(0, 12)) {
      const level = parseInt(el.tagName[1], 10);
      if (lastLevel > 0 && level > lastLevel + 1) {
        issues.push(`skip-level:h${lastLevel}-to-h${level}`);
        break;
      }
      lastLevel = level;
    }
    return { h1, h2, h3, issues };
  });
}

/** @param {import('@playwright/test').Page} page */
async function auditFocusVisible(page) {
  return page.evaluate(() => {
    const focusable = document.querySelector(
      'button:not([disabled]), a[href], input:not([disabled]), [tabindex]:not([tabindex="-1"])',
    );
    if (!focusable) return { tested: false, hasVisibleRing: false };
    focusable.focus();
    const s = window.getComputedStyle(focusable);
    const outline = s.outlineWidth;
    const boxShadow = s.boxShadow;
    const hasRing =
      (outline && outline !== "0px" && outline !== "none") ||
      (boxShadow && boxShadow !== "none" && boxShadow.length > 4);
    return { tested: true, hasVisibleRing: hasRing, tag: focusable.tagName.toLowerCase() };
  });
}

/** @param {import('@playwright/test').Page} page @param {number} maxSamples */
async function sampleColorContrast(page, maxSamples = 5) {
  return page.evaluate((limit) => {
    /** @param {string} css */
    function parseRgb(css) {
      const m = css.match(/rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)/);
      if (!m) return null;
      return [parseFloat(m[1]), parseFloat(m[2]), parseFloat(m[3])];
    }

    /** @param {number[]} fg @param {number[]} bg */
    function luminance(fg, bg) {
      const rel = (c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
      const L = (rgb) => 0.2126 * rel(rgb[0] / 255) + 0.7152 * rel(rgb[1] / 255) + 0.0722 * rel(rgb[2] / 255);
      const l1 = L(fg);
      const l2 = L(bg);
      const lighter = Math.max(l1, l2);
      const darker = Math.min(l1, l2);
      return (lighter + 0.05) / (darker + 0.05);
    }

    const samples = [];
    const selectors = ["[data-testid='page-title']", "h1", "h2", ".btn-primary", "main p"];
    for (const sel of selectors) {
      const el = document.querySelector(sel);
      if (!el) continue;
      const s = window.getComputedStyle(el);
      const fg = parseRgb(s.color);
      const bg = parseRgb(s.backgroundColor);
      if (!fg || !bg) continue;
      const ratio = luminance(fg, bg);
      samples.push({ selector: sel, ratio: Math.round(ratio * 100) / 100, pass: ratio >= 3 });
      if (samples.length >= limit) break;
    }
    return samples;
  }, maxSamples);
}

/** @param {import('@playwright/test').Page} page */
async function auditSpacingTokens(page) {
  return page.evaluate(() => {
    const main = document.querySelector("main") || document.body;
    const s = window.getComputedStyle(main);
    const gapClasses = new Set();
    const paddingClasses = new Set();
    for (const el of main.querySelectorAll("[class*='gap-'], [class*='p-'], [class*='px-'], [class*='py-']")) {
      const cn = el.className.toString();
      const gaps = cn.match(/\bgap-(0|0\.5|1|1\.5|2|2\.5|3|4|5|6|8|10|12)\b/g);
      const pads = cn.match(/\b(p|px|py|pt|pb|pl|pr)-(0|0\.5|1|1\.5|2|2\.5|3|4|5|6|8|10|12)\b/g);
      if (gaps) gaps.forEach((g) => gapClasses.add(g));
      if (pads) pads.forEach((p) => paddingClasses.add(p));
    }
    return {
      mainPadding: s.padding,
      gapTokens: [...gapClasses].slice(0, 12),
      paddingTokens: [...paddingClasses].slice(0, 12),
    };
  });
}

/** @param {import('@playwright/test').Page} page */
async function auditLandmarks(page) {
  return page.evaluate(() => ({
    main: !!document.querySelector("main"),
    nav: !!document.querySelector("nav"),
    header: !!document.querySelector("header"),
  }));
}

const KANBAN_BOARDS = [
  { path: "/appointments", boardTestId: "appointments-kanban", label: "Appointments" },
  { path: "/tasks", boardTestId: "followups-kanban", label: "Follow-ups" },
  { path: "/campaigns", boardTestId: "campaigns-kanban", label: "Campaigns" },
];

const TABLE_PAGES = [
  { path: "/patients", tableSelector: "table", searchTestId: "patients-search" },
  { path: "/staff", tableSelector: "table", searchTestId: "staff-search" },
  { path: "/doctors", tableSelector: "table" },
  { path: "/settings/users", tableSelector: "table", searchTestId: "users-search" },
  { path: "/missed-revenue", tableSelector: "table" },
];

const DASHBOARD_STAT_CARDS = [
  "dashboard-appointments-overview",
  "dashboard-upcoming-appointments",
  "dashboard-revenue-overview",
  "dashboard-ai-insights",
];

const MODAL_TRIGGERS = [
  { path: "/patients", triggerTestId: "new-patient-btn", dialogTestId: "new-patient-dialog", label: "New Patient" },
  { path: "/appointments", triggerTestId: "new-appt-btn", dialogTestId: null, label: "New Appointment" },
  { path: "/campaigns", triggerTestId: "new-campaign-btn", dialogTestId: null, label: "New Campaign" },
  { path: "/settings/users", triggerTestId: "new-user-btn", dialogTestId: null, label: "New User" },
  { path: "/staff", triggerTestId: "new-staff-profile-btn", dialogTestId: null, label: "New Staff" },
  { path: "/tasks", triggerTestId: "new-task-btn", dialogTestId: null, label: "New Task" },
];

const GLASS_SURFACE_CHECKS = [
  { selector: ".glass-sidebar", label: "sidebar", expectGlass: true },
  { selector: ".glass-card", label: "primary-card", expectGlass: true },
  { selector: "main .solid-card", label: "solid-card", expectGlass: false },
];

const TYPOGRAPHY_CHECKS = ["title-font", "title-color", "body-font", "caption-size"];
const SPACING_CHECKS = ["border-radius-mix", "main-padding", "gap-tokens"];
const A11Y_CHECKS = ["heading-hierarchy", "aria-threshold", "focus-visible", "contrast-sample", "landmarks"];
const ROUTE_VIEWPORT_DIMENSIONS = [
  "layout-no-overflow",
  "page-chrome",
  "title-font",
  "title-color",
  "sidebar-glass",
  "no-broken-images",
  "button-variants",
  "h1-min-size",
  "console-clean",
  "main-surface",
];

/**
 * Scenario count math for DESIGN_TEST_MATRIX.md
 * @param {{ routeCount?: number }} opts
 */
function computeScenarioCounts(opts = {}) {
  const routes = opts.routeCount ?? 21;
  const vp = VIEWPORTS.length;
  const rv = routes * vp;

  return {
    routeViewportMatrix: rv * ROUTE_VIEWPORT_DIMENSIONS.length,
    screenshotRegression: rv,
    glassSurfaces: rv * GLASS_SURFACE_CHECKS.length + KANBAN_BOARDS.length * vp * 2,
    typographySpacing: rv * (TYPOGRAPHY_CHECKS.length + SPACING_CHECKS.length),
    accessibilityDesign: rv * A11Y_CHECKS.length,
    componentsKanban: KANBAN_BOARDS.length * vp * 5,
    componentsStatCards: DASHBOARD_STAT_CARDS.length * vp * 3,
    componentsTables: TABLE_PAGES.length * vp * 4,
    componentsModals: MODAL_TRIGGERS.length * vp * 2,
    componentsSidebar: NAV_ITEMS.length * vp * 2,
    patientTabs: PATIENT_TABS.length * vp * 2,
    legacyDesignSpecs: 8 + 8 + 12 + 19,
    get total() {
      return (
        this.routeViewportMatrix
        + this.screenshotRegression
        + this.glassSurfaces
        + this.typographySpacing
        + this.accessibilityDesign
        + this.componentsKanban
        + this.componentsStatCards
        + this.componentsTables
        + this.componentsModals
        + this.componentsSidebar
        + this.patientTabs
        + this.legacyDesignSpecs
      );
    },
    inputs: { routes, viewports: vp, routeViewportProduct: rv },
  };
}

module.exports = {
  VIEWPORTS,
  PRIMARY_TEXT_RGB,
  SAMPLE_MODE,
  getStaticDesignRoutes,
  getDesignRoutes,
  routeSlug,
  viewportSlug,
  gotoDesignRoute,
  captureDesignScreenshot,
  auditHeadingHierarchy,
  auditFocusVisible,
  sampleColorContrast,
  auditSpacingTokens,
  auditLandmarks,
  KANBAN_BOARDS,
  TABLE_PAGES,
  DASHBOARD_STAT_CARDS,
  MODAL_TRIGGERS,
  GLASS_SURFACE_CHECKS,
  TYPOGRAPHY_CHECKS,
  SPACING_CHECKS,
  A11Y_CHECKS,
  ROUTE_VIEWPORT_DIMENSIONS,
  NAV_ITEMS,
  PATIENT_TABS,
  computeScenarioCounts,
};

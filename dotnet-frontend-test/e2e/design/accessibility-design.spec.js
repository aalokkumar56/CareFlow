const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const {
  VIEWPORTS,
  getDesignRoutes,
  routeSlug,
  viewportSlug,
  gotoDesignRoute,
  A11Y_CHECKS,
} = require("../helpers/design-matrix");
const {
  auditHeadingHierarchy,
  auditFocusVisible,
  sampleColorContrast,
  auditLandmarks,
} = require("../helpers/design-matrix");
const { countIconButtonsWithoutAriaLabel } = require("../helpers/ui-audit");

/** Max unlabeled icon buttons — aligned with accessibility-labels.spec.js */
const ARIA_THRESHOLDS = {
  "/": 2,
  "/patients": 5,
  "/appointments": 4,
  "/tasks": 3,
  "/campaigns": 4,
  "/staff": 3,
  "/doctors": 3,
  "/settings/users": 2,
  "/settings/notifications": 0,
};

test.describe.configure({ mode: "serial" });

test.describe("Accessibility design — route × viewport", () => {
  for (const viewport of VIEWPORTS) {
    for (const routePath of getDesignRoutes({})) {
      if (routePath === "/login") continue;

      for (const check of A11Y_CHECKS) {
        test(`${routeSlug(routePath)}@${viewportSlug(viewport)} — ${check}`, async ({ page }) => {
          await login(page);
          await gotoDesignRoute(page, routePath, viewport);

          if (check === "heading-hierarchy") {
            const hierarchy = await auditHeadingHierarchy(page);
            expect(hierarchy.issues.length).toBeLessThanOrEqual(1);
          }

          if (check === "aria-threshold") {
            const { count } = await countIconButtonsWithoutAriaLabel(page);
            const threshold = ARIA_THRESHOLDS[routePath] ?? 4;
            expect(count).toBeLessThanOrEqual(threshold);
          }

          if (check === "focus-visible") {
            const focus = await auditFocusVisible(page);
            if (focus.tested) {
              expect(focus.hasVisibleRing).toBe(true);
            }
          }

          if (check === "contrast-sample") {
            const samples = await sampleColorContrast(page, 4);
            const failures = samples.filter((s) => !s.pass);
            expect(failures.length).toBeLessThanOrEqual(2);
          }

          if (check === "landmarks") {
            const landmarks = await auditLandmarks(page);
            expect(landmarks.main || landmarks.nav).toBe(true);
          }
        });
      }
    }
  }
});

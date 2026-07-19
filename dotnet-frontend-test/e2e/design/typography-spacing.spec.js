const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const {
  VIEWPORTS,
  PRIMARY_TEXT_RGB,
  getDesignRoutes,
  routeSlug,
  viewportSlug,
  gotoDesignRoute,
  TYPOGRAPHY_CHECKS,
  SPACING_CHECKS,
} = require("../helpers/design-matrix");
const {
  auditBorderRadiusMix,
  auditElementTypography,
  auditBodyTypography,
} = require("../helpers/ui-audit");
const { auditSpacingTokens } = require("../helpers/design-matrix");
const { reportBug } = require("../helpers/bug-log");

test.describe.configure({ mode: "serial" });

test.describe("Typography & spacing tokens — route × viewport", () => {
  for (const viewport of VIEWPORTS) {
    for (const routePath of getDesignRoutes({})) {
      if (routePath === "/login") continue;

      for (const check of TYPOGRAPHY_CHECKS) {
        test(`${routeSlug(routePath)}@${viewportSlug(viewport)} — ${check}`, async ({ page }) => {
          await login(page);
          await gotoDesignRoute(page, routePath, viewport);

          if (check === "title-font") {
            const typo = await auditElementTypography(page, "[data-testid='page-title']");
            if (!typo) return;
            expect(typo.fontFamily.toLowerCase()).toMatch(/outfit|heading|inter/);
          }

          if (check === "title-color") {
            const typo = await auditElementTypography(page, "[data-testid='page-title']");
            if (!typo) return;
            expect(typo.color).toBe(PRIMARY_TEXT_RGB);
          }

          if (check === "body-font") {
            const body = await auditBodyTypography(page);
            expect(body.fontFamily.toLowerCase()).toMatch(/inter|system-ui|sans-serif/);
          }

          if (check === "caption-size") {
            const caption = page.locator(".text-ui-caption, .text-ui-label").first();
            if ((await caption.count()) === 0) return;
            const size = await caption.evaluate((el) => parseFloat(window.getComputedStyle(el).fontSize));
            expect(size).toBeGreaterThanOrEqual(10);
            expect(size).toBeLessThanOrEqual(14);
          }
        });
      }

      for (const check of SPACING_CHECKS) {
        test(`${routeSlug(routePath)}@${viewportSlug(viewport)} — ${check}`, async ({ page }) => {
          await login(page);
          await gotoDesignRoute(page, routePath, viewport);

          if (check === "border-radius-mix") {
            const radii = await auditBorderRadiusMix(page);
            const hasSm = radii.includes("rounded-sm");
            const hasXl = radii.includes("rounded-xl");
            const has2xl = radii.includes("rounded-2xl");
            if ((hasSm && hasXl) || (hasSm && has2xl)) {
              reportBug({
                severity: "Low",
                page: routePath,
                title: `Mixed border-radius at ${viewport.id}`,
                actual: radii.join(", "),
              });
            }
          }

          if (check === "main-padding") {
            const spacing = await auditSpacingTokens(page);
            expect(spacing.mainPadding).toBeTruthy();
          }

          if (check === "gap-tokens") {
            const spacing = await auditSpacingTokens(page);
            expect(spacing.gapTokens.length + spacing.paddingTokens.length).toBeGreaterThan(0);
          }
        });
      }
    }
  }
});

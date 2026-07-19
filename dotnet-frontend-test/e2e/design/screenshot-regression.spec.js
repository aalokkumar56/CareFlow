const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const {
  VIEWPORTS,
  getDesignRoutes,
  routeSlug,
  viewportSlug,
  gotoDesignRoute,
  captureDesignScreenshot,
} = require("../helpers/design-matrix");

test.describe.configure({ mode: "serial" });

// Entire suite ignored via playwright.config.js testIgnore — kept for optional manual runs.
test.describe.skip("Screenshot regression — route × viewport", () => {
  for (const viewport of VIEWPORTS) {
    for (const routePath of getDesignRoutes({})) {
      test(`screenshot ${routeSlug(routePath)} @ ${viewportSlug(viewport)}`, async ({ page }) => {
        if (routePath !== "/login") await login(page);
        await gotoDesignRoute(page, routePath, viewport);

        if (routePath !== "/login") {
          await expect(page).not.toHaveURL(/\/login/);
        }

        const name = `${routeSlug(routePath)}--${viewportSlug(viewport)}`;
        const file = await captureDesignScreenshot(page, "design/screenshots", name);
        expect(file).toContain("screenshots");
      });
    }
  }
});

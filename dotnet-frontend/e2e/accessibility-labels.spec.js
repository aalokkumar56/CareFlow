const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { MAIN_ROUTES, SETTINGS_ROUTES } = require("./helpers/routes");
const { countIconButtonsWithoutAriaLabel } = require("./helpers/ui-audit");

/**
 * Max unlabeled icon-only buttons per route.
 * Pages marked 0 were fixed for accessibility; others allow known baseline until cleaned up.
 */
const UNLABELED_BUTTON_THRESHOLDS = {
  "/": 2,
  "/appointments": 4,
  "/patients": 5,
  "/inbox": 3,
  "/email-inbox": 3,
  "/campaigns": 4,
  "/missed-revenue": 2,
  "/tasks": 3,
  "/doctors": 3,
  "/staff": 3,
  "/settings/users": 2,
  "/settings/roles": 1,
  "/settings/permissions": 1,
  "/settings/templates": 2,
  "/settings/notifications": 0,
  "/settings/hospital": 1,
  "/settings/integrations": 1,
  "/notifications/preferences": 0,
  "/login": 0,
};

const AUDIT_ROUTES = [
  ...MAIN_ROUTES.map((r) => ({ path: r.path, label: r.label })),
  ...SETTINGS_ROUTES.map((r) => ({ path: r.path, label: r.label })),
  { path: "/login", label: "Login" },
];

test.describe("Aria-label census (admin + login)", () => {
  test("login page has zero unlabeled icon buttons", async ({ page }) => {
    await page.goto("/login");
    await page.waitForLoadState("domcontentloaded");
    const { count, offenders } = await countIconButtonsWithoutAriaLabel(page);
    expect(count, `Login unlabeled buttons: ${offenders.join(", ")}`).toBe(0);
  });

  for (const route of AUDIT_ROUTES.filter((r) => r.path !== "/login")) {
    test(`${route.label} (${route.path}) — unlabeled icon buttons within threshold`, async ({ page }) => {
      await login(page);
      await page.goto(route.path, { waitUntil: "networkidle" });
      await page.waitForTimeout(400);

      if (page.url().includes("/login")) {
        test.skip(true, `Redirected to login for ${route.path}`);
        return;
      }

      const { count, offenders } = await countIconButtonsWithoutAriaLabel(page);
      const threshold = UNLABELED_BUTTON_THRESHOLDS[route.path] ?? 3;

      expect(
        count,
        `${route.path}: ${count} unlabeled icon buttons (threshold ${threshold}). Offenders: ${offenders.join("; ")}`,
      ).toBeLessThanOrEqual(threshold);

      if (threshold === 0) {
        expect(count, `Fixed page ${route.path} should have zero unlabeled buttons`).toBe(0);
      }
    });
  }
});

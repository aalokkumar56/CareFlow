const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");

const ROUTES = [
  { path: "/", titleSelector: "[data-testid='page-title']" },
  { path: "/patients", titleSelector: "[data-testid='page-title']" },
  { path: "/appointments", titleSelector: "[data-testid='page-title']" },
  { path: "/tasks", titleSelector: "[data-testid='page-title']" },
  { path: "/doctors", titleSelector: "[data-testid='page-title']" },
  { path: "/missed-revenue", titleSelector: "[data-testid='page-title']" },
  { path: "/settings/users", titleSelector: "[data-testid='page-title']" },
  { path: "/settings/notifications", titleSelector: "[data-testid='page-title']" },
];

const PRIMARY_TEXT = "rgb(2, 44, 34)";

test.describe("Design tokens", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  for (const { path, titleSelector } of ROUTES) {
    test(`typography tokens on ${path}`, async ({ page }) => {
      await page.goto(path, { waitUntil: "domcontentloaded" });
      const title = page.locator(titleSelector).first();
      await expect(title).toBeVisible({ timeout: 20_000 });

      const styles = await title.evaluate((el) => {
        const s = window.getComputedStyle(el);
        return { fontFamily: s.fontFamily, color: s.color };
      });

      expect(styles.fontFamily.toLowerCase()).toContain("outfit");
      expect(styles.color).toBe(PRIMARY_TEXT);
    });
  }
});

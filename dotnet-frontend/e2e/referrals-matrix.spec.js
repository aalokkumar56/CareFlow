const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { gotoAsRole } = require("./helpers/navigation");
const {
  ALL_ROLES_MATRIX,
  SEARCH_QUERIES,
  DOCTOR_DETAIL_TABS,
  VIEWPORTS,
  routeAllowed,
} = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../src/lib/permissions");
const { apiLogin, apiRequest } = require("./helpers/api");

test.describe.configure({ mode: "serial" });

const referralRoute = { permission: PERMISSIONS.ReferralView };

test.describe("Referrals matrix — CRM list × doctor detail × roles", () => {
  let roles = {};
  let doctorId;

  test.beforeAll(async () => {
    roles = await getRoleSessions();
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    try {
      const doctors = await apiRequest(admin.accessToken, "GET", "/doctors");
      const list = Array.isArray(doctors) ? doctors : doctors?.items || doctors?.data || [];
      if (list.length > 0) doctorId = list[0].id;
    } catch { /* optional */ }
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const query of SEARCH_QUERIES) {
      test(`${role}: doctors list search "${query || "(empty)"}"`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(referralRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        const listPromise = page.waitForResponse((r) => r.url().includes("/api/doctors") && r.ok());
        await page.goto("/doctors", { waitUntil: "domcontentloaded" });
        await listPromise;

        const search = page.getByPlaceholder(/search/i).first();
        if (!(await search.isVisible().catch(() => false))) return;

        const searchPromise = page.waitForResponse(
          (r) => r.url().includes("/api/doctors") && r.ok(),
          { timeout: 15_000 },
        );
        await search.fill(query);
        await searchPromise;
      });
    }

    for (const tab of DOCTOR_DETAIL_TABS) {
      test(`${role}: doctor detail tab ${tab.testId}`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(referralRoute, session.permissions) || !doctorId);

        await login(page, { email: session.email, password: session.password });
        await page.goto(`/doctors/${doctorId}`, { waitUntil: "networkidle" });
        await page.getByTestId(tab.testId).click();
        await expect(page.getByTestId(tab.testId)).toHaveAttribute("data-state", "active");
      });
    }

    for (const viewport of VIEWPORTS) {
      test(`${role} @ ${viewport.name}: referral CRM loads`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(referralRoute, session.permissions));

        await gotoAsRole(page, session, "/doctors", { viewport });
        expect(new URL(page.url()).pathname).toBe("/doctors");
      });
    }
  }
});

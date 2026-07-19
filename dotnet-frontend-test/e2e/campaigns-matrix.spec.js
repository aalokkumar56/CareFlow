const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { gotoAsRole } = require("./helpers/navigation");
const {
  ALL_ROLES_MATRIX,
  KANBAN_PAGES,
  VIEWPORTS,
  routeAllowed,
} = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

const campaignRoute = { permission: PERMISSIONS.CampaignView };
const campaignKanban = KANBAN_PAGES[2];

test.describe("Campaigns matrix — kanban columns × viewports × roles", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const column of campaignKanban.columns) {
      test(`${role}: campaign column "${column}"`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(campaignRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        const listPromise = page.waitForResponse((r) => r.url().includes("/api/campaigns") && r.ok());
        await page.goto("/campaigns", { waitUntil: "domcontentloaded" });
        await listPromise;

        await expect(page.getByTestId(campaignKanban.testId)).toBeVisible();
        await expect(page.getByText(column, { exact: false }).first()).toBeVisible({ timeout: 10_000 });
      });
    }

    for (const viewport of VIEWPORTS) {
      test(`${role} @ ${viewport.name}: campaigns kanban`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(campaignRoute, session.permissions));

        await gotoAsRole(page, session, "/campaigns", { viewport });
        await expect(page.getByTestId(campaignKanban.testId)).toBeVisible({ timeout: 20_000 });
      });
    }
  }
});

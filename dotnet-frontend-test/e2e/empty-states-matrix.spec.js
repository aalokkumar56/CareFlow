const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { ALL_ROLES_MATRIX, EMPTY_SEARCH_PAGES, routeAllowed } = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

/** Map list paths to permission checks. */
const PAGE_PERMISSIONS = {
  "/patients": { permission: PERMISSIONS.PatientView },
  "/appointments": { permission: PERMISSIONS.AppointmentView },
  "/tasks": { permission: PERMISSIONS.DashboardView },
  "/staff": { anyPermission: [PERMISSIONS.StaffView, PERMISSIONS.ClinicalView] },
  "/inbox": { permission: PERMISSIONS.ConversationView },
  "/settings/users": { permission: PERMISSIONS.UserView },
  "/doctors": { permission: PERMISSIONS.ReferralView },
  "/campaigns": { permission: PERMISSIONS.CampaignView },
};

test.describe("Empty states matrix — no-result search per role", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const pageDef of EMPTY_SEARCH_PAGES) {
      test(`${role}: empty search on ${pageDef.path}`, async ({ page }) => {
        const session = roles[role];
        const permRoute = PAGE_PERMISSIONS[pageDef.path];
        test.skip(permRoute && !routeAllowed(permRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto(pageDef.path, { waitUntil: "networkidle" });

        if (pageDef.testId) {
          const searchPromise = page.waitForResponse(
            (r) => r.url().includes("/api/") && r.ok(),
            { timeout: 15_000 },
          );
          await page.getByTestId(pageDef.testId).fill(pageDef.query);
          await searchPromise;

          const rows = page.locator("[data-testid^='patient-row-'], [data-testid^='followup-card-'], tr, [data-testid^='conv-']");
          const count = await rows.count();
          expect(count).toBeLessThanOrEqual(2);
        } else {
          const search = page.getByPlaceholder(/search/i).first();
          if (await search.isVisible().catch(() => false)) {
            await search.fill(pageDef.query);
            await page.waitForTimeout(500);
          }
        }
      });
    }
  }
});

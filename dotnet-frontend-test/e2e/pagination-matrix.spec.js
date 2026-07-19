const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { ALL_ROLES_MATRIX, LIST_SEARCH_PAGES, routeAllowed } = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

const PAGE_PERMS = {
  "/patients": { permission: PERMISSIONS.PatientView },
  "/appointments": { permission: PERMISSIONS.AppointmentView },
  "/tasks": { permission: PERMISSIONS.DashboardView },
  "/staff": { anyPermission: [PERMISSIONS.StaffView, PERMISSIONS.ClinicalView] },
  "/inbox": { permission: PERMISSIONS.ConversationView },
  "/settings/users": { permission: PERMISSIONS.UserView },
};

const PAGINATION_ACTIONS = ["initial-load", "search-refetch", "clear-search"];

test.describe("Pagination matrix — list API refetch actions × roles", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const listPage of LIST_SEARCH_PAGES) {
      for (const action of PAGINATION_ACTIONS) {
        test(`${role}: ${listPage.path} ${action}`, async ({ page }) => {
          const perm = PAGE_PERMS[listPage.path];
          const session = roles[role];
          test.skip(perm && !routeAllowed(perm, session.permissions));

          await login(page, { email: session.email, password: session.password });

          const initialPromise = page.waitForResponse(
            (r) => r.url().includes(listPage.apiFragment) && r.ok(),
            { timeout: 30_000 },
          );
          await page.goto(listPage.path, { waitUntil: "domcontentloaded" });
          const initial = await initialPromise;
          expect(initial.ok()).toBeTruthy();

          if (action === "initial-load") return;

          const search = page.getByTestId(listPage.testId);
          if (!(await search.isVisible().catch(() => false))) return;

          if (action === "search-refetch") {
            const refetch = page.waitForResponse(
              (r) => r.url().includes(listPage.apiFragment) && r.ok(),
              { timeout: 15_000 },
            );
            await search.fill("e2e");
            await refetch;
          }

          if (action === "clear-search") {
            await search.fill("e2e");
            await page.waitForTimeout(300);
            const clearRefetch = page.waitForResponse(
              (r) => r.url().includes(listPage.apiFragment) && r.ok(),
              { timeout: 15_000 },
            );
            await search.fill("");
            await clearRefetch;
          }
        });
      }
    }
  }
});

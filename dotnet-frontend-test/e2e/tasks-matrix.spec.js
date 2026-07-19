const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const {
  ALL_ROLES_MATRIX,
  SEARCH_QUERIES,
  KANBAN_PAGES,
  routeAllowed,
} = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

const tasksRoute = { permission: PERMISSIONS.DashboardView };
const tasksKanban = KANBAN_PAGES[1];

test.describe("Tasks matrix — follow-ups search × kanban × roles", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const query of SEARCH_QUERIES) {
      test(`${role}: task search "${query || "(empty)"}"`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(tasksRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/tasks", { waitUntil: "networkidle" });

        const searchPromise = page.waitForResponse(
          (r) => r.url().includes("/api/tasks") && r.ok(),
          { timeout: 15_000 },
        );
        await page.getByTestId("task-search").fill(query);
        await searchPromise;
      });
    }

    for (const column of tasksKanban.columns) {
      test(`${role}: follow-up column "${column}"`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(tasksRoute, session.permissions));

        await login(page, { email: session.email, password: session.password });
        await page.goto("/tasks", { waitUntil: "networkidle" });
        await expect(page.getByTestId(tasksKanban.testId)).toBeVisible();
        await expect(page.getByText(column, { exact: false }).first()).toBeVisible({ timeout: 10_000 });
      });
    }
  }
});

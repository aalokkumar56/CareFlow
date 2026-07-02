const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { getRoleSessions } = require("./helpers/role-session");
const { gotoAsRole } = require("./helpers/navigation");
const {
  ALL_ROLES_MATRIX,
  SEARCH_QUERIES,
  VIEWPORTS,
  routeAllowed,
} = require("./helpers/test-matrix");
const { PERMISSIONS } = require("../src/lib/permissions");

test.describe.configure({ mode: "serial" });

const inboxRoute = { permission: PERMISSIONS.ConversationView };

const INBOX_PAGES = [
  { path: "/inbox", searchTestId: "inbox-search", apiFragment: "/api/conversations", label: "WhatsApp" },
  { path: "/email-inbox", searchTestId: null, apiFragment: "/api/email", label: "Email" },
];

test.describe("Inbox matrix — WhatsApp + email × search × roles × viewports", () => {
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  for (const role of ALL_ROLES_MATRIX) {
    for (const inbox of INBOX_PAGES) {
      for (const query of SEARCH_QUERIES) {
        test(`${role}: ${inbox.label} search "${query || "(empty)"}"`, async ({ page }) => {
          const session = roles[role];
          test.skip(!routeAllowed(inboxRoute, session.permissions));

          await login(page, { email: session.email, password: session.password });
          await page.goto(inbox.path, { waitUntil: "networkidle" });

          if (!inbox.searchTestId) return;
          const search = page.getByTestId(inbox.searchTestId);
          if (!(await search.isVisible().catch(() => false))) return;

          const searchPromise = page.waitForResponse(
            (r) => r.url().includes(inbox.apiFragment) && r.ok(),
            { timeout: 15_000 },
          ).catch(() => null);
          await search.fill(query);
          if (searchPromise) await searchPromise;
        });
      }

      for (const viewport of VIEWPORTS) {
        test(`${role} @ ${viewport.name}: ${inbox.label} inbox loads`, async ({ page }) => {
          const session = roles[role];
          test.skip(!routeAllowed(inboxRoute, session.permissions));

          await gotoAsRole(page, session, inbox.path, { viewport });
          expect(new URL(page.url()).pathname).toBe(inbox.path);
        });
      }
    }
  }
});

const { test, expect } = require("@playwright/test");
const { getRoleSessions } = require("./helpers/role-session");
const { gotoAsRole } = require("./helpers/navigation");
const {
  STATIC_APP_ROUTES,
  DETAIL_ROUTE_SEEDS,
  VIEWPORTS,
  ALL_ROLES_MATRIX,
  routeAllowed,
} = require("./helpers/test-matrix");
const { apiLogin, apiRequest, createPatient } = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");

test.describe.configure({ mode: "serial" });

test.describe("Navigation matrix — routes × roles × viewports", () => {
  /** @type {Record<string, { email: string, password: string, permissions: string[] }>} */
  let roles = {};
  /** @type {{ patientId?: string, doctorId?: string, campaignId?: string }} */
  let seeds = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const stamp = Date.now();
    const patient = await createPatient(admin.accessToken, `Nav Matrix ${stamp}`, E2E_TEST_PHONE);
    seeds.patientId = patient.id || patient.patient?.id;

    try {
      const doctors = await apiRequest(admin.accessToken, "GET", "/doctors");
      const list = Array.isArray(doctors) ? doctors : doctors?.items || doctors?.data || [];
      if (list.length > 0) seeds.doctorId = list[0].id;
    } catch { /* optional */ }

    try {
      const campaigns = await apiRequest(admin.accessToken, "GET", "/campaigns");
      const list = Array.isArray(campaigns) ? campaigns : campaigns?.items || campaigns?.data || [];
      if (list.length > 0) seeds.campaignId = list[0].id;
    } catch { /* optional */ }
  });

  const protectedRoutes = STATIC_APP_ROUTES.filter((r) => !r.public);

  for (const role of ALL_ROLES_MATRIX) {
    for (const route of protectedRoutes) {
      for (const viewport of VIEWPORTS) {
        test(`${role} @ ${viewport.name}: ${route.label} loads or redirects`, async ({ page }) => {
          const session = roles[role];
          const allowed = routeAllowed(route, session.permissions);

          const { pathname } = await gotoAsRole(page, session, route.path, {
            viewport,
            waitForApi: allowed && route.apiPath ? `/api${route.apiPath}` : undefined,
          });

          if (!allowed) {
            expect(pathname).not.toBe(route.path);
            return;
          }

          expect(pathname).toBe(route.path);
          if (route.expectTestId) {
            await expect(page.getByTestId(route.expectTestId)).toBeVisible({ timeout: 20_000 });
          }
        });
      }
    }

    for (const detail of DETAIL_ROUTE_SEEDS) {
      const id = seeds[detail.seedKey];
      if (!id) continue;

      const path = detail.pathTemplate.replace(":id", id);
      for (const viewport of VIEWPORTS) {
        test(`${role} @ ${viewport.name}: ${detail.label} loads or redirects`, async ({ page }) => {
          const session = roles[role];
          const allowed = routeAllowed(detail, session.permissions);

          const { pathname } = await gotoAsRole(page, session, path, { viewport });
          if (!allowed) {
            expect(pathname).not.toBe(path);
            return;
          }

          expect(pathname).toBe(path);
          if (detail.expectTestId) {
            await expect(page.getByTestId(detail.expectTestId)).toBeVisible({ timeout: 20_000 });
          }
        });
      }
    }
  }
});

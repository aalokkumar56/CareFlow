const { test, expect } = require("@playwright/test");
const { getRoleSessions } = require("./helpers/role-session");
const { login } = require("./helpers/auth");
const {
  STATIC_APP_ROUTES,
  DETAIL_ROUTE_SEEDS,
  ALL_ROLES_MATRIX,
  routeAllowed,
} = require("./helpers/test-matrix");
const { apiLogin, apiRequest, createPatient } = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");

test.describe.configure({ mode: "serial" });

test.describe("API assertions matrix — list/detail responses per role", () => {
  let roles = {};
  let seeds = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const stamp = Date.now();
    const patient = await createPatient(admin.accessToken, `API Matrix ${stamp}`, E2E_TEST_PHONE);
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

  const apiRoutes = STATIC_APP_ROUTES.filter((r) => !r.public && r.apiPath);

  for (const role of ALL_ROLES_MATRIX) {
    for (const route of apiRoutes) {
      test(`${role}: GET ${route.label} (${route.path}) via UI navigation`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(route, session.permissions), `${role} lacks permission for ${route.label}`);

        await login(page, { email: session.email, password: session.password });

        const apiResponse = page.waitForResponse(
          (r) => r.url().includes(`/api${route.apiPath}`) && r.request().method() === "GET" && r.ok(),
          { timeout: 30_000 },
        );
        await page.goto(route.path, { waitUntil: "domcontentloaded" });
        const res = await apiResponse;
        expect(res.ok()).toBeTruthy();

        const contentType = res.headers()["content-type"] || "";
        if (contentType.includes("json")) {
          const body = await res.json();
          expect(body).toBeTruthy();
        }
      });
    }

    for (const detail of DETAIL_ROUTE_SEEDS) {
      const id = seeds[detail.seedKey];
      if (!id || !detail.apiPath) continue;
      const path = detail.pathTemplate.replace(":id", id);

      test(`${role}: GET ${detail.apiPath}/${id} on detail page`, async ({ page }) => {
        const session = roles[role];
        test.skip(!routeAllowed(detail, session.permissions), `${role} lacks permission`);

        await login(page, { email: session.email, password: session.password });
        const apiResponse = page.waitForResponse(
          (r) => r.url().includes(`/api${detail.apiPath}/${id}`) && r.ok(),
          { timeout: 30_000 },
        );
        await page.goto(path, { waitUntil: "domcontentloaded" });
        const res = await apiResponse;
        expect(res.ok()).toBeTruthy();
      });
    }
  }
});

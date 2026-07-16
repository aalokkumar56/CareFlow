const { test, expect } = require("@playwright/test");
const { loginAsHospital } = require("./helpers/auth");
const { apiRequest, registerTenant, apiLogin } = require("./helpers/api");
const { protectedStaticRoutes } = require("./helpers/test-matrix");
const {
  ensureMultiHospitals,
  getPatientOrNull,
  patientSearchCount,
  HOSPITALS,
  HOSPITAL_A,
  HOSPITAL_B,
  HOSPITAL_C,
  loadMultiHospitalCache,
} = require("./helpers/multi-hospital");

const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";

test.describe.configure({ mode: "serial" });

const HOSPITAL_KEYS = HOSPITALS.map((h) => h.key);

test.describe("Multi-hospital Phase 1 — per-tenant admin flows", () => {
  /** @type {Awaited<ReturnType<typeof ensureMultiHospitals>>} */
  let hospitals = {};

  test.beforeAll(async () => {
    // global-setup already logged in once; stagger to avoid auth rate limit (10/min).
    const cached = loadMultiHospitalCache();
    if (cached) {
      await new Promise((resolve) => setTimeout(resolve, 6000));
    }
    hospitals = await ensureMultiHospitals({ staggerMs: 3000 });
    for (const key of HOSPITAL_KEYS) {
      expect(
        hospitals[key]?.patientId,
        `${key} exclusive patient missing — restart API to re-seed MultiHospitalE2eSeeder`,
      ).toBeTruthy();
    }
  });

  test.describe("Cross-tenant isolation", () => {
    test("API: tenant cannot read another tenant patient by ID", async () => {
      const keys = HOSPITAL_KEYS;
      const pairs = keys.map((key, i) => [hospitals[keys[i]], hospitals[keys[(i + 1) % keys.length]]]);
      for (const [viewer, other] of pairs) {
        const crossRead = await getPatientOrNull(viewer.accessToken, other.patientId);
        expect(crossRead, `${viewer.key} read ${other.key} patient`).toBeNull();
      }
    });

    test("API: patient search scoped per hospital", async () => {
      for (const hospital of HOSPITALS) {
        const ctx = hospitals[hospital.key];
        expect(await patientSearchCount(ctx.accessToken, hospital.exclusivePatient)).toBeGreaterThan(0);
        for (const other of HOSPITALS) {
          if (other.key === hospital.key) continue;
          expect(
            await patientSearchCount(ctx.accessToken, other.exclusivePatient),
            `${hospital.key} must not see ${other.key} patient`,
          ).toBe(0);
        }
      }
    });

    test("UI: Hospital A patients list hides Hospital B and C patients", async ({ page }) => {
      const alpha = hospitals[HOSPITAL_A.key];
      const others = [hospitals[HOSPITAL_B.key], hospitals[HOSPITAL_C.key]];

      await loginAsHospital(page, HOSPITAL_A.key);
      await page.goto("/patients");
      await expect(page.getByTestId("patients-search")).toBeVisible({ timeout: 20_000 });

      await page.getByTestId("patients-search").fill(alpha.exclusivePatient);
      await expect(page.getByText(alpha.exclusivePatient)).toBeVisible({ timeout: 15_000 });

      for (const other of others) {
        await page.getByTestId("patients-search").fill(other.exclusivePatient);
        await expect(page.getByText(other.exclusivePatient)).toHaveCount(0, { timeout: 10_000 });
      }
    });
  });

  for (const hospitalKey of HOSPITAL_KEYS) {
    test.describe(`${hospitalKey} admin`, () => {
      test("login", async ({ page }) => {
        const hospital = hospitals[hospitalKey];
        await loginAsHospital(page, hospitalKey);
        await expect(page).not.toHaveURL(/\/login/);
      });

      test("dashboard loads with correct hospital name", async ({ page }) => {
        const hospital = hospitals[hospitalKey];
        await loginAsHospital(page, hospitalKey);
        await page.goto("/");
        await expect(page.getByTestId("page-title")).toBeVisible({ timeout: 20_000 });
        // Dashboard variant hides hospital-name-badge; name appears in subtitle instead.
        await expect(
          page.getByText(`Here's what's happening at ${hospital.name} today.`),
        ).toBeVisible({ timeout: 15_000 });
      });

      test("patients list shows own patients only", async ({ page }) => {
        const hospital = hospitals[hospitalKey];
        const otherKey = hospitalKey === HOSPITAL_A.key ? HOSPITAL_B.key : HOSPITAL_A.key;
        const other = hospitals[otherKey];

        await loginAsHospital(page, hospitalKey);
        await page.goto("/patients");
        await expect(page.getByTestId("patients-search")).toBeVisible();

        await page.getByTestId("patients-search").fill(hospital.exclusivePatient);
        await expect(page.getByText(hospital.exclusivePatient)).toBeVisible({ timeout: 15_000 });

        await page.getByTestId("patients-search").fill(other.exclusivePatient);
        await expect(page.getByText(other.exclusivePatient)).toHaveCount(0, { timeout: 10_000 });
      });

      test("patient detail loads for seeded patient", async ({ page }) => {
        const hospital = hospitals[hospitalKey];
        await loginAsHospital(page, hospitalKey);
        await page.goto(`/patients/${hospital.patientId}`, { waitUntil: "domcontentloaded" });
        await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
        await expect(page.getByTestId("patient-name")).toContainText(hospital.exclusivePatient);
      });

      test("appointments page loads", async ({ page }) => {
        await loginAsHospital(page, hospitalKey);
        await page.goto("/appointments");
        await expect(page.getByTestId("appointments-kanban")).toBeVisible({ timeout: 20_000 });
      });

      test("inbox page loads", async ({ page }) => {
        await loginAsHospital(page, hospitalKey);
        await page.goto("/inbox");
        await expect(page.getByTestId("inbox-search")).toBeVisible({ timeout: 20_000 });
      });

      test("settings hospital profile shows tenant name", async ({ page }) => {
        const hospital = hospitals[hospitalKey];
        await loginAsHospital(page, hospitalKey);
        await page.goto("/settings/hospital");
        await expect(page.getByTestId("sidebar-hospital-name")).toContainText(hospital.name, {
          timeout: 20_000,
        });
      });

      test("staff page loads", async ({ page }) => {
        await loginAsHospital(page, hospitalKey);
        await page.goto("/staff");
        await expect(page.getByTestId("staff-search")).toBeVisible({ timeout: 20_000 });
      });

      test("settings users page loads", async ({ page }) => {
        await loginAsHospital(page, hospitalKey);
        await page.goto("/settings/users");
        await expect(page.getByTestId("users-search")).toBeVisible({ timeout: 20_000 });
      });

      test("API dashboard overview returns tenant data", async () => {
        const hospital = hospitals[hospitalKey];
        const overview = await apiRequest(hospital.accessToken, "GET", "/dashboard/overview");
        expect(overview).toBeTruthy();
      });

      test("API audit logs scoped to tenant", async () => {
        const hospital = hospitals[hospitalKey];
        const logs = await apiRequest(hospital.accessToken, "GET", "/audit-logs?limit=50");
        expect(Array.isArray(logs)).toBe(true);
        for (const other of HOSPITALS) {
          if (other.key === hospitalKey) continue;
          const otherLogs = await apiRequest(hospitals[other.key].accessToken, "GET", "/audit-logs?limit=50");
          const otherActions = new Set((otherLogs || []).map((l) => l.action));
          const overlap = (logs || []).filter((l) => otherActions.has(l.action) && l.action?.startsWith("e2e-audit-isolation"));
          expect(overlap).toHaveLength(0);
        }
      });
    });
  }

  test.describe("Test matrix — core routes per hospital", () => {
    const matrixRoutes = protectedStaticRoutes().filter((r) =>
      ["/", "/patients", "/appointments", "/inbox", "/staff", "/settings/hospital"].includes(r.path),
    );

    for (const hospitalKey of HOSPITAL_KEYS) {
      for (const route of matrixRoutes) {
        test(`${hospitalKey}: ${route.label} (${route.path})`, async ({ page }) => {
          await loginAsHospital(page, hospitalKey);
          await page.goto(route.path);
          if (route.expectTestId) {
            await expect(page.getByTestId(route.expectTestId)).toBeVisible({ timeout: 20_000 });
          } else {
            await expect(page).not.toHaveURL(/\/login/);
          }
        });
      }
    }
  });

  test.describe("Register tenant via UI", () => {
    test("signup page registers a new hospital and can login", async ({ page }) => {
      const stamp = Date.now();
      const hospitalName = `E2E Clinic ${stamp}`;
      const adminEmail = `admin+e2e-${stamp}@cureflow.test`;
      const password = "Test@12345";

      await page.goto("/signup");
      await expect(page.getByTestId("signup-form")).toBeVisible({ timeout: 15_000 });

      await page.getByTestId("signup-hospital-name").fill(hospitalName);
      await page.getByTestId("signup-admin-name").fill("E2E Admin");
      await page.getByTestId("signup-email").fill(adminEmail);
      await page.getByTestId("signup-password").fill(password);
      await page.getByTestId("signup-submit").click();

      await expect(page).toHaveURL(/\/pending-approval/, { timeout: 20_000 });
      await expect(page.getByTestId("pending-approval-title")).toBeVisible({ timeout: 20_000 });
    });
  });

  test.describe("Register tenant via API", () => {
    test("register-tenant creates isolated hospital", async () => {
      const stamp = Date.now();
      const adminEmail = `api+e2e-${stamp}@cureflow.test`;
      const tenant = await registerTenant({
        hospitalName: `API Hospital ${stamp}`,
        adminName: "API Admin",
        adminEmail,
        adminPassword: "Test@12345",
      });
      expect(tenant.slug).toBeTruthy();
      expect(tenant.lifecycle_status).toBe("pending_approval");

      const auth = await apiLogin(adminEmail, "Test@12345");
      expect(auth.tenant.lifecycle_status).toBe("pending_approval");

      const session = await apiRequest(auth.accessToken, "GET", "/auth/session");
      expect(session.tenant.lifecycle_status).toBe("pending_approval");

      const blocked = await fetch(`${apiURL}/api/dashboard/overview`, {
        headers: { Authorization: `Bearer ${auth.accessToken}` },
      });
      expect(blocked.status).toBe(403);
    });
  });

  test.describe("WhatsApp webhook tenant routing", () => {
    test("demo inbound routes to correct tenant by phone_number_id", async () => {
      for (const hospital of HOSPITALS) {
        const ctx = hospitals[hospital.key];
        const phoneNumberId = `e2e-${hospital.slug}`;
        const message = `webhook-routing-${hospital.key}-${Date.now()}`;

        const res = await fetch(`${apiURL}/api/whatsapp/demo/inbound`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            message,
            phone: hospital.patientPhone,
            phone_number_id: phoneNumberId,
          }),
        });
        expect(res.ok, `${hospital.key} demo inbound`).toBeTruthy();

        await new Promise((r) => setTimeout(r, 1500));

        const conversations = await apiRequest(ctx.accessToken, "GET", "/conversations?page=1&page_size=20");
        const items = conversations?.items || conversations || [];
        const match = items.some(
          (c) =>
            (c.last_message_preview || c.lastMessagePreview || "").includes(message) ||
            (c.wa_phone || c.waPhone || "").includes(hospital.patientPhone.slice(-10)),
        );
        expect(match, `${hospital.key} should see routed conversation`).toBeTruthy();
      }
    });
  });

  test.describe("Platform ops console", () => {
    test("platform tenants page lists hospitals", async ({ page }) => {
      const { loginAsPlatformOps } = require("./helpers/platform-auth");
      await loginAsPlatformOps(page);
      await page.getByTestId("platform-tab-all").click();
      await expect(page.getByTestId("platform-tenant-care-cure-althan")).toBeVisible({ timeout: 15_000 });
      await expect(page.getByTestId("platform-tenant-city-hospital-surat")).toBeVisible();
      await expect(page.getByTestId("platform-tenant-metro-clinic-pune")).toBeVisible();
    });
  });
});

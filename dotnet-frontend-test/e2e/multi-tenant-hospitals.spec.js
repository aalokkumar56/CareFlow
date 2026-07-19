const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiRequest } = require("./helpers/api");
const {
  ensureMultiHospitals,
  getPatientOrNull,
  patientSearchCount,
  HOSPITALS,
} = require("./helpers/multi-hospital");
const { STATIC_APP_ROUTES } = require("./helpers/test-matrix");

test.describe.configure({ mode: "serial" });

test.describe("Multi-tenant hospitals — isolation and core flows", () => {
  /** @type {Awaited<ReturnType<typeof ensureMultiHospitals>>} */
  let hospitals = {};

  test.beforeAll(async () => {
    hospitals = await ensureMultiHospitals({ staggerMs: 2500 });
    for (const h of HOSPITALS) {
      expect(hospitals[h.key]?.patientId, `${h.key} exclusive patient missing — restart API to re-seed`).toBeTruthy();
    }
  });

  test("API: tenant A cannot read tenant B patient by ID", async () => {
    const althan = hospitals.althan;
    const surat = hospitals.surat;

    expect(await getPatientOrNull(althan.accessToken, surat.patientId)).toBeNull();
    expect(await getPatientOrNull(surat.accessToken, althan.patientId)).toBeNull();
  });

  test("API: patient search is scoped to own hospital", async () => {
    const { althan, surat, pune } = hospitals;

    expect(await patientSearchCount(althan.accessToken, "Althan Exclusive")).toBeGreaterThan(0);
    expect(await patientSearchCount(althan.accessToken, "Surat Exclusive")).toBe(0);
    expect(await patientSearchCount(althan.accessToken, "Pune Exclusive")).toBe(0);

    expect(await patientSearchCount(surat.accessToken, "Surat Exclusive")).toBeGreaterThan(0);
    expect(await patientSearchCount(surat.accessToken, "Althan Exclusive")).toBe(0);

    expect(await patientSearchCount(pune.accessToken, "Pune Exclusive")).toBeGreaterThan(0);
    expect(await patientSearchCount(pune.accessToken, "Althan Exclusive")).toBe(0);
  });

  for (const hospitalKey of ["althan", "surat"]) {
    test(`UI: ${hospitalKey} admin — dashboard and patients show own data only`, async ({ page }) => {
      const hospital = hospitals[hospitalKey];
      const otherKey = hospitalKey === "althan" ? "surat" : "althan";
      const other = hospitals[otherKey];

      await login(page, { email: hospital.adminEmail, password: hospital.password });

      await page.goto("/");
      await expect(page.getByTestId("page-title")).toBeVisible({ timeout: 20_000 });

      await page.goto("/patients");
      await expect(page.getByTestId("patients-search")).toBeVisible();
      await page.getByTestId("patients-search").fill(hospital.exclusivePatient);
      await expect(page.getByText(hospital.exclusivePatient)).toBeVisible({ timeout: 15_000 });

      await page.getByTestId("patients-search").fill(other.exclusivePatient);
      await expect(page.getByText(other.exclusivePatient)).toHaveCount(0, { timeout: 10_000 });
    });

    test(`UI: ${hospitalKey} admin — appointments and inbox load`, async ({ page }) => {
      const hospital = hospitals[hospitalKey];
      await login(page, { email: hospital.adminEmail, password: hospital.password });

      await page.goto("/appointments");
      await expect(page.getByTestId("appointments-kanban")).toBeVisible({ timeout: 20_000 });

      await page.goto("/inbox");
      await expect(page.getByTestId("inbox-search")).toBeVisible({ timeout: 20_000 });
    });

    test(`UI: ${hospitalKey} admin — settings hospital profile matches tenant`, async ({ page }) => {
      const hospital = hospitals[hospitalKey];
      await login(page, { email: hospital.adminEmail, password: hospital.password });

      await page.goto("/settings/hospital");
      await expect(page.getByTestId("sidebar-hospital-name")).toContainText(hospital.name, {
        timeout: 20_000,
      });
    });
  }

  test("API: each hospital dashboard overview returns tenant-scoped data", async () => {
    for (const key of ["althan", "surat", "pune"]) {
      const overview = await apiRequest(hospitals[key].accessToken, "GET", "/dashboard/overview");
      expect(overview).toBeTruthy();
    }
  });

  test("API: hospital departments differ per tenant", async () => {
    const althanDepts = await apiRequest(hospitals.althan.accessToken, "GET", "/hospital-profile/departments");
    const suratDepts = await apiRequest(hospitals.surat.accessToken, "GET", "/hospital-profile/departments");

    const althanNames = (Array.isArray(althanDepts) ? althanDepts : []).map((d) =>
      typeof d === "string" ? d : d?.name,
    );
    const suratNames = (Array.isArray(suratDepts) ? suratDepts : []).map((d) =>
      typeof d === "string" ? d : d?.name,
    );

    expect(althanNames).toContain("Cardiology");
    expect(suratNames).toContain("Orthopedics");
    expect(althanNames).not.toEqual(suratNames);
  });

  test("UI: pune admin — core route matrix smoke", async ({ page }) => {
    const hospital = hospitals.pune;
    await login(page, { email: hospital.adminEmail, password: hospital.password });

    const routes = STATIC_APP_ROUTES.filter(
      (r) => !r.public && r.expectTestId && ["/", "/patients", "/appointments", "/inbox", "/staff", "/tasks"].includes(r.path),
    );

    for (const route of routes) {
      await page.goto(route.path, { waitUntil: "domcontentloaded" });
      await expect(page.getByTestId(route.expectTestId)).toBeVisible({ timeout: 20_000 });
    }
  });
});

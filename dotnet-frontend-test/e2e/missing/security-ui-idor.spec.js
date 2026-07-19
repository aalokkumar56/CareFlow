/**
 * UI security — cross-tenant IDOR patient URLs + session after logout.
 *
 * Scenario IDs (TEST_SCENARIOS.md):
 *   UI-SEC-041 … UI-SEC-044 (session after logout)
 *   UI-SEC-049 (IDOR patient URL)
 *   UI-SEC-055 (sequential ID guessing does not leak names)
 *   UI-SEC-056 (cross-tenant WhatsApp conversation id)
 */
const { test, expect } = require("@playwright/test");
const { login, loginAsHospital } = require("../helpers/auth");
const { apiLogin, createPatient } = require("../helpers/api");
const {
  ensureMultiHospitals,
  HOSPITALS,
  getPatientOrNull,
} = require("../helpers/multi-hospital");

test.describe.configure({ mode: "serial" });

function patientIdFrom(created) {
  return created?.id || created?.patient?.id;
}

async function storageAuthKeys(page) {
  return page.evaluate(() => ({
    token: localStorage.getItem("cureflow_token"),
    user: localStorage.getItem("cureflow_user"),
    tenant: localStorage.getItem("cureflow_tenant"),
  }));
}

test.describe("Security UI — IDOR patient URLs + session after logout", () => {
  /** @type {Awaited<ReturnType<typeof ensureMultiHospitals>>} */
  let hospitals = {};
  let ownPatientId;
  let ownPatientName;

  test.beforeAll(async () => {
    hospitals = await ensureMultiHospitals({ staggerMs: 2500 });
    for (const h of HOSPITALS.slice(0, 2)) {
      expect(
        hospitals[h.key]?.patientId,
        `${h.key} exclusive patient missing — restart API to re-seed`,
      ).toBeTruthy();
    }

    const admin = await apiLogin("admin@cureflow.in", "admin123");
    ownPatientName = `E2E IDOR Own ${Date.now()}`;
    const created = await createPatient(admin.accessToken, ownPatientName);
    ownPatientId = patientIdFrom(created);
    expect(ownPatientId).toBeTruthy();
  });

  test("UI-SEC-049: tenant A cannot open tenant B patient URL (no PHI)", async ({ page }) => {
    const althan = hospitals.althan;
    const surat = hospitals.surat;

    // API baseline — cross-tenant GET must not return the patient.
    expect(await getPatientOrNull(althan.accessToken, surat.patientId)).toBeNull();

    await loginAsHospital(page, "althan");
    await page.goto(`/patients/${surat.patientId}`, { waitUntil: "domcontentloaded" });

    // Never render the other tenant's exclusive patient name / PHI.
    await expect(page.getByText(surat.exclusivePatient)).toHaveCount(0);
    await expect(page.getByTestId("patient-name")).toHaveCount(0);

    const bodyText = (await page.locator("body").innerText()).toLowerCase();
    expect(bodyText).not.toContain(surat.exclusivePatient.toLowerCase());
    expect(bodyText).not.toContain(String(surat.patientPhone || "").slice(-10));

    // Still authenticated (not bounced to login by the forbidden patient id).
    await expect(page).not.toHaveURL(/\/login/);

    // Own-tenant data still reachable when the shell is up.
    const patientsNav = await page.goto("/patients", { waitUntil: "domcontentloaded" }).catch(() => null);
    if (patientsNav) {
      await expect(page.getByTestId("patients-search")).toBeVisible({ timeout: 20_000 });
      await page.getByTestId("patients-search").fill(althan.exclusivePatient);
      await expect(page.getByText(althan.exclusivePatient)).toBeVisible({ timeout: 15_000 });
    }
  });

  test("UI-SEC-055: sequential ID guess does not leak other-tenant name in breadcrumb", async ({
    page,
  }) => {
    const althan = hospitals.althan;
    const surat = hospitals.surat;

    await loginAsHospital(page, "althan");
    await page.goto(`/patients/${surat.patientId}`, { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(1_500);

    const breadcrumb = page.getByTestId("page-breadcrumb");
    if (await breadcrumb.isVisible().catch(() => false)) {
      await expect(breadcrumb).not.toContainText(surat.exclusivePatient);
    }
    const title = await page.title();
    expect(title.toLowerCase()).not.toContain(surat.exclusivePatient.toLowerCase());
    await expect(page.getByText(surat.exclusivePatient)).toHaveCount(0);
  });

  test("UI-SEC-056: WhatsApp conversation id from tenant B not openable in tenant A inbox", async ({
    page,
  }) => {
    const althan = hospitals.althan;
    const surat = hospitals.surat;

    const suratConvs = await fetch(
      `${process.env.PLAYWRIGHT_API_URL || "http://localhost:5180"}/api/conversations?page=1&page_size=5`,
      { headers: { Authorization: `Bearer ${surat.accessToken}` } },
    );
    if (!suratConvs.ok) {
      test.skip(true, `Surat conversations unavailable: ${suratConvs.status}`);
      return;
    }
    const suratData = await suratConvs.json();
    const suratItems = suratData?.items || [];
    const foreignConvId = suratItems[0]?.id;
    test.skip(!foreignConvId, "No Surat conversation to probe");

    await loginAsHospital(page, "althan");
    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("inbox-search")).toBeVisible({ timeout: 20_000 });

    // Foreign conversation must not appear in Althan list.
    await expect(page.getByTestId(`conv-${foreignConvId}`)).toHaveCount(0);

    // Direct API probe with Althan token must not return Surat PHI.
    const probe = await fetch(
      `${process.env.PLAYWRIGHT_API_URL || "http://localhost:5180"}/api/conversations/${foreignConvId}`,
      { headers: { Authorization: `Bearer ${althan.accessToken}` } },
    );
    expect([403, 404]).toContain(probe.status);
    if (probe.ok) {
      const payload = await probe.json();
      const name =
        payload?.patient?.name ||
        payload?.conversation?.display_name ||
        payload?.conversation?.name ||
        "";
      expect(String(name)).not.toContain(surat.exclusivePatient);
    }
  });

  test("UI-SEC-041: logout clears cureflow_token / user / tenant from localStorage", async ({
    page,
  }) => {
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 20_000 });

    const before = await storageAuthKeys(page);
    expect(before.token).toBeTruthy();
    expect(before.user).toBeTruthy();

    await page.getByTestId("logout-btn").click();
    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });

    const after = await storageAuthKeys(page);
    expect(after.token).toBeNull();
    expect(after.user).toBeNull();
    expect(after.tenant).toBeNull();
  });

  test("UI-SEC-042: after logout, Back cannot show authenticated patient PHI", async ({ page }) => {
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto(`/patients/${ownPatientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("patient-name")).toHaveText(ownPatientName);

    await page.goto("/", { waitUntil: "domcontentloaded" });
    await page.getByTestId("logout-btn").click();
    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });

    await page.goBack();
    await page.waitForTimeout(1_000);

    // Must land on login (or empty) — never re-render the patient name from history cache.
    await expect(page.getByText(ownPatientName)).toHaveCount(0);
    const keys = await storageAuthKeys(page);
    expect(keys.token).toBeNull();
  });

  test("UI-SEC-043: after logout, protected routes redirect to login", async ({ page }) => {
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.getByTestId("logout-btn").click();
    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });

    for (const route of ["/", "/patients", `/patients/${ownPatientId}`, "/inbox", "/settings/users"]) {
      await page.goto(route, { waitUntil: "domcontentloaded" });
      await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });
      await expect(page.getByTestId("login-email")).toBeVisible({ timeout: 10_000 });
    }
  });

  test("UI-SEC-044: after logout, API calls from cleared session do not keep PHI UI", async ({
    page,
  }) => {
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto(`/patients/${ownPatientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("logout-btn").click();
    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });

    // Simulate a lingering fetch with no token — UI must stay on login.
    const probeStatus = await page.evaluate(async (id) => {
      const res = await fetch(`/api/patients/${id}`, { credentials: "same-origin" });
      return res.status;
    }, ownPatientId);

    expect([401, 403]).toContain(probeStatus);
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByText(ownPatientName)).toHaveCount(0);
  });
});

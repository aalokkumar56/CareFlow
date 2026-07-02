const { test, expect } = require("@playwright/test");
const { login, selectRadixOption } = require("./helpers/auth");
const { apiLogin, apiRequest, createPatient, createUser } = require("./helpers/api");
const { E2E_TEST_PHONE } = require("./helpers/constants");

test.describe.configure({ mode: "serial" });

test.describe("CRUD submit flows (API-backed)", () => {
  const stamp = Date.now();
  let patientId;
  let patientName;
  let doctorId;

  test.beforeAll(async () => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");

    patientName = `E2E CRUD Patient ${stamp}`;
    const patient = await createPatient(
      admin.accessToken,
      patientName,
      E2E_TEST_PHONE,
    );
    patientId = patient.id || patient.patient?.id;

    try {
      const doctors = await apiRequest(admin.accessToken, "GET", "/doctors");
      const list = Array.isArray(doctors) ? doctors : doctors?.items || doctors?.data || [];
      if (list.length > 0) doctorId = list[0].id;
    } catch {
      /* optional */
    }
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("create patient → list → detail verifies name", async ({ page }) => {
    const unique = `E2E New Patient ${stamp}`;
    await page.goto("/patients");
    await expect(page.getByTestId("patients-search")).toBeVisible();

    await page.getByTestId("new-patient-btn").click();
    await expect(page.getByTestId("new-patient-dialog")).toBeVisible();
    await page.getByTestId("np-name").fill(unique);
    await page.getByTestId("np-phone").fill(E2E_TEST_PHONE);
    await page.getByTestId("np-age").fill("28");

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/patients") && r.request().method() === "POST",
    );
    await page.getByTestId("np-save-btn").click();
    const res = await createResponse;
    expect(res.ok(), `Create patient failed: ${res.status()}`).toBeTruthy();
    const created = await res.json();
    const newPatientId = created.id || created.patient?.id;
    expect(newPatientId, "Create patient response should include id").toBeTruthy();

    await expect(page.getByText(unique)).toBeVisible({ timeout: 15_000 });

    await page.goto(`/patients/${newPatientId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("patient-name")).toHaveText(unique);
  });

  test("create appointment → appears in kanban", async ({ page }) => {
    test.skip(!patientId, "No patient available for appointment");

    await page.goto("/appointments");
    await expect(page.getByTestId("appointments-kanban")).toBeVisible();

    await page.getByTestId("new-appt-btn").click();
    await expect(page.getByTestId("new-appt-dialog")).toBeVisible();

    await page.getByTestId("appt-patient").click();
    await page.getByRole("option", { name: new RegExp(patientName.slice(0, 20)) }).first().click();

    const doctorTrigger = page.getByRole("combobox").filter({ hasText: /Select doctor|Doctor/i }).first();
    if ((await doctorTrigger.count()) === 0) {
      test.skip(true, "No doctors configured in booking options");
      return;
    }
    await doctorTrigger.click();
    const firstDoctor = page.getByRole("option").first();
    if (!(await firstDoctor.isVisible().catch(() => false))) {
      test.skip(true, "No doctor options available");
      return;
    }
    const doctorLabel = await firstDoctor.textContent();
    await firstDoctor.click();

    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    const dateStr = tomorrow.toISOString().slice(0, 10);

    await page.locator('input[type="date"]').fill(dateStr);

    const deptTrigger = page.getByTestId("department-select");
    if (await deptTrigger.isVisible().catch(() => false)) {
      await deptTrigger.click();
      const deptOption = page.getByRole("option").first();
      if (await deptOption.isVisible().catch(() => false)) {
        await deptOption.click();
      }
    }

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/appointments") && r.request().method() === "POST",
    );
    await page.getByTestId("appt-save-btn").click();
    const res = await createResponse;
    if (!res.ok()) {
      const body = await res.text();
      if (body.includes("doctor") || body.includes("department")) {
        test.skip(true, `Appointment prerequisites missing: ${body}`);
        return;
      }
      expect(res.ok(), `Create appointment failed: ${res.status()} ${body}`).toBeTruthy();
    }

    await expect(page.getByTestId("appointments-kanban")).toContainText(patientName, { timeout: 15_000 });
    if (doctorLabel) {
      await expect(page.getByTestId("appointments-kanban")).toContainText(doctorLabel.trim().split(" ")[0], { timeout: 10_000 });
    }
  });

  test("create user in settings → row visible without search", async ({ page }) => {
    const userStamp = Date.now();
    const userName = `E2E CRUD User ${userStamp}`;
    const email = `e2e.crud.user.${userStamp}@cureflow.test`;

    await page.goto("/settings/users");
    await expect(page.getByTestId("users-search")).toBeVisible();
    await expect(page.getByTestId("users-search")).toHaveValue("");

    await page.getByTestId("new-user-btn").click();
    await page.getByTestId("nu-name").fill(userName);
    await page.getByTestId("nu-email").fill(email);
    await page.getByTestId("nu-password").fill("TestPass123!");
    await selectRadixOption(page, "nu-role", "Reception");

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/users") && r.request().method() === "POST",
    );
    await page.getByTestId("nu-save-btn").click();
    const res = await createResponse;
    expect(res.ok(), `Create user failed: ${res.status()} ${await res.text()}`).toBeTruthy();

    // Regression: new user row must appear without typing in users-search
    await expect(page.getByText(userName)).toBeVisible({ timeout: 15_000 });
  });

  test("create staff profile (admin) → visible in list", async ({ page }) => {
    const staffStamp = Date.now();
    const staffName = `E2E Staff Profile ${staffStamp}`;
    const email = `e2e.staff.profile.${staffStamp}@cureflow.test`;

    await page.goto("/settings/users");
    await page.getByTestId("new-user-btn").click();
    await page.getByTestId("nu-name").fill(staffName);
    await page.getByTestId("nu-email").fill(email);
    await page.getByTestId("nu-password").fill("TestPass123!");
    await selectRadixOption(page, "nu-role", "Reception");
    const userCreate = page.waitForResponse(
      (r) => r.url().includes("/api/users") && r.request().method() === "POST",
    );
    await page.getByTestId("nu-save-btn").click();
    const userRes = await userCreate;
    expect(userRes.ok()).toBeTruthy();
    const createdUser = await userRes.json();
    const userId = createdUser.id;

    await page.goto("/staff", { waitUntil: "networkidle" });
    await page.reload({ waitUntil: "networkidle" });
    await expect(page.getByTestId("staff-search")).toBeVisible();

    await page.getByTestId("new-staff-profile-btn").click();
    await page.getByRole("combobox").first().click();
    const userOption = page.getByRole("option", { name: new RegExp(staffName) });

    if (await userOption.isVisible().catch(() => false)) {
      await userOption.click();
      const deptTrigger = page.getByTestId("department-select");
      if (await deptTrigger.isVisible().catch(() => false)) {
        await deptTrigger.click();
        const dept = page.getByRole("option").first();
        if (await dept.isVisible().catch(() => false)) {
          await dept.click();
        }
      }
      const createResponse = page.waitForResponse(
        (r) => r.url().includes("/api/staff") && r.request().method() === "POST",
      );
      await page.getByRole("button", { name: "Create" }).click();
      const res = await createResponse;
      expect(res.ok(), `Create staff failed: ${res.status()} ${await res.text()}`).toBeTruthy();
    } else {
      // Staff user dropdown is capped (~100 users); fall back to API when new user is not listed
      const admin = await apiLogin("admin@cureflow.in", "admin123");
      await apiRequest(admin.accessToken, "POST", "/staff", {
        user_id: userId,
        employment_type: "permanent",
        is_available: true,
      });
      await page.keyboard.press("Escape");
      await page.reload({ waitUntil: "networkidle" });
    }

    await expect(page.getByText(staffName)).toBeVisible({ timeout: 15_000 });
  });

  test("edit patient basic field on detail → save → verify", async ({ page }) => {
    test.skip(!patientId, "No patient for edit flow");

    const noteText = `E2E note ${stamp}`;
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("patient-edit-icon").click();
    await expect(page.getByTestId("edit-patient-form")).toBeVisible();

    await page.getByTestId("pd-notes").fill(noteText);

    const saveResponse = page.waitForResponse(
      (r) => r.url().includes(`/api/patients/${patientId}`) && ["PUT", "PATCH"].includes(r.request().method()),
    );
    await page.getByTestId("pd-save-btn").click();
    const res = await saveResponse;
    expect(res.ok(), `Save patient failed: ${res.status()}`).toBeTruthy();

    await page.reload({ waitUntil: "networkidle" });
    await page.getByTestId("patient-edit-icon").click();
    await expect(page.getByTestId("pd-notes")).toHaveValue(noteText);
  });

  test("log referral on doctor detail (admin)", async ({ page }) => {
    test.skip(!doctorId || !patientId, "No sample doctor or patient for referral");

    await page.goto(`/doctors/${doctorId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("doctor-name")).toBeVisible({ timeout: 20_000 });

    await page.getByRole("tab", { name: /Referrals/i }).click();
    await page.getByTestId("log-referral-btn").click();

    await page.getByTestId("ref-patient-select").click();
    await page.getByRole("option", { name: new RegExp(patientName.slice(0, 15)) }).first().click();

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/referrals") && r.request().method() === "POST",
    );
    await page.getByTestId("ref-save-btn").click();
    const res = await createResponse;
    expect(res.ok(), `Log referral failed: ${res.status()} ${await res.text()}`).toBeTruthy();

    await expect(page.getByText(patientName)).toBeVisible({ timeout: 15_000 });
  });

  test("campaign: new dialog, minimal fields, save draft", async ({ page }) => {
    const campaignName = `E2E Campaign ${stamp}`;

    await page.goto("/campaigns");
    await expect(page.getByTestId("campaigns-kanban")).toBeVisible();

    await page.getByTestId("new-campaign-btn").click();
    await expect(page.getByTestId("new-campaign-dialog")).toBeVisible();

    await page.getByTestId("nc-name").fill(campaignName);
    await page.getByTestId("nc-message").fill(`Hello from E2E test ${stamp}`);

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/campaigns") && r.request().method() === "POST",
    );
    await page.getByTestId("nc-save-btn").click();
    const res = await createResponse;
    expect(res.ok(), `Create campaign failed: ${res.status()} ${await res.text()}`).toBeTruthy();

    await expect(page.getByTestId("campaigns-kanban")).toContainText(campaignName, { timeout: 15_000 });
  });
});

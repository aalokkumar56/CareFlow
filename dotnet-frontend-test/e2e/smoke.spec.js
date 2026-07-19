const { test, expect } = require("@playwright/test");
const { login, selectRadixOption } = require("./helpers/auth");
const { E2E_TEST_PHONE } = require("./helpers/constants");

test.describe("Cure-Flow critical paths", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("login and dashboard loads", async ({ page }) => {
    await expect(page).not.toHaveURL(/\/login/);
    await page.goto("/");
    await expect(page.getByTestId("page-title")).toBeVisible();
  });

  test("patients list and create patient", async ({ page }) => {
    await page.goto("/patients");
    await expect(page.getByTestId("patients-search")).toBeVisible();

    const unique = `E2E Patient ${Date.now()}`;
    await page.getByTestId("new-patient-btn").click();
    await expect(page.getByTestId("new-patient-dialog")).toBeVisible();
    await page.getByTestId("np-name").fill(unique);
    await page.getByTestId("np-phone").fill(E2E_TEST_PHONE);
    await page.getByTestId("np-age").fill("32");

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/patients") && r.request().method() === "POST"
    );
    await page.getByTestId("np-save-btn").click();
    const res = await createResponse;
    expect(res.ok(), `Create patient failed: ${res.status()}`).toBeTruthy();
    await expect(page.getByText(unique)).toBeVisible({ timeout: 15_000 });
  });

  test("appointments list loads", async ({ page }) => {
    await page.goto("/appointments");
    await expect(page.getByTestId("appointments-kanban")).toBeVisible();
    await expect(page.getByTestId("new-appt-btn")).toBeVisible();
  });

  test("staff page shows department options", async ({ page }) => {
    await page.goto("/staff");
    await expect(page.getByTestId("staff-search")).toBeVisible();
    await page.getByTestId("new-staff-profile-btn").click();
    await page.getByTestId("department-select").click();
    const options = page.getByRole("option");
    await expect(options.first()).toBeVisible();
    const count = await options.count();
    expect(count).toBeGreaterThan(0);
    const disabledOnly = await page.getByRole("option", { name: "No departments configured" }).count();
    expect(disabledOnly).toBe(0);
  });

  test("settings users create with role selection", async ({ page }) => {
    await page.goto("/settings/users");
    await expect(page.getByTestId("users-search")).toBeVisible();

    const stamp = Date.now();
    const email = `e2e.user.${stamp}@cureflow.test`;

    await page.getByTestId("new-user-btn").click();
    await page.getByTestId("nu-name").fill(`E2E User ${stamp}`);
    await page.getByTestId("nu-email").fill(email);
    await page.getByTestId("nu-password").fill("TestPass123!");

    await selectRadixOption(page, "nu-role", "Nurse");

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/users") && r.request().method() === "POST"
    );
    await page.getByTestId("nu-save-btn").click();
    const res = await createResponse;
    expect(res.ok(), `Create user failed: ${res.status()} ${await res.text()}`).toBeTruthy();
    await page.getByTestId("users-search").fill(`E2E User ${stamp}`);
    await expect(page.getByText(`E2E User ${stamp}`)).toBeVisible({ timeout: 15_000 });
  });

  test("campaigns suggested drafts section", async ({ page }) => {
    await page.goto("/campaigns");
    await expect(page.getByTestId("suggested-drafts-section")).toBeVisible();
    await expect(page.getByTestId("campaigns-kanban")).toBeVisible();
  });

  test("follow-ups tasks kanban", async ({ page }) => {
    await page.goto("/tasks");
    await expect(page.getByTestId("followups-kanban")).toBeVisible();
    await expect(page.getByTestId("new-task-btn")).toBeVisible();
  });
});

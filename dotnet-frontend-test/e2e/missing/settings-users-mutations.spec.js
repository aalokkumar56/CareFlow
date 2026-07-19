const { test, expect } = require("@playwright/test");
const { login, selectRadixOption } = require("../helpers/auth");
const { apiLogin, createUser, apiRequest } = require("../helpers/api");

test.describe.configure({ mode: "serial" });

test.describe("Settings users — edit & reset password", () => {
  const stamp = Date.now();
  const email = `e2e.users.mut.${stamp}@cureflow.test`;
  const password = "TestPass123!";
  let userName = `E2E Users Mut ${stamp}`;
  let userId;

  test.beforeAll(async () => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const created = await createUser(admin.accessToken, {
      name: userName,
      email,
      password,
      role: "reception",
    });
    userId = created.id || created.user?.id;
    expect(userId, "Seeded user should have an id").toBeTruthy();
  });

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("edit user name and role persist after reload", async ({ page }) => {
    const updatedName = `E2E Users Edited ${stamp}`;

    await page.goto("/settings/users");
    await expect(page.getByTestId("users-search")).toBeVisible();
    await page.getByTestId("users-search").fill(email);
    await expect(page.getByText(userName)).toBeVisible({ timeout: 15_000 });

    const row = page
      .locator("div.border-b")
      .filter({ hasText: email })
      .filter({ has: page.getByRole("button", { name: "Edit" }) })
      .first();
    await row.getByRole("button", { name: "Edit" }).click();

    const dialog = page.getByRole("dialog", { name: /Edit User/i });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByTestId("nu-name")).toHaveValue(userName);

    await dialog.getByTestId("nu-name").fill(updatedName);
    await selectRadixOption(page, "nu-role", "Nurse");

    const patchResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/users/${userId}`) &&
        ["PATCH", "PUT"].includes(r.request().method()),
    );
    await dialog.getByRole("button", { name: "Save" }).click();
    const res = await patchResponse;
    expect(res.ok(), `Update user failed: ${res.status()} ${await res.text()}`).toBeTruthy();

    userName = updatedName;

    await page.reload({ waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("users-search")).toBeVisible();
    await page.getByTestId("users-search").fill(email);
    await expect(page.getByText(updatedName)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/Nurse/i).first()).toBeVisible();

    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const listed = await apiRequest(admin.accessToken, "GET", `/users?q=${encodeURIComponent(email)}&limit=20`);
    const items = Array.isArray(listed) ? listed : listed?.items || [];
    const match = items.find((u) => u.id === userId || u.email === email);
    expect(match, "Updated user should appear in API list").toBeTruthy();
    expect(match.name).toBe(updatedName);
    expect(String(match.role).toLowerCase()).toMatch(/nurse/);
  });

  test("reset password generates temporary password and allows login", async ({ page }) => {
    await page.goto("/settings/users");
    await expect(page.getByTestId("users-search")).toBeVisible();
    await page.getByTestId("users-search").fill(email);
    await expect(page.getByText(userName)).toBeVisible({ timeout: 15_000 });

    const row = page
      .locator("div.border-b")
      .filter({ hasText: email })
      .filter({ has: page.getByRole("button", { name: "Reset password" }) })
      .first();
    await row.getByRole("button", { name: "Reset password" }).click();

    const dialog = page.getByRole("dialog", { name: /Reset Password/i });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText(userName)).toBeVisible();

    const resetResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/users/${userId}/reset-password`) &&
        r.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: "Reset" }).click();
    const res = await resetResponse;
    expect(res.ok(), `Reset password failed: ${res.status()} ${await res.text()}`).toBeTruthy();
    const body = await res.json();
    const tempPassword = body.temporary_password || body.temporaryPassword;
    expect(tempPassword, "API should return temporary_password").toBeTruthy();

    await expect(dialog.locator("code")).toHaveText(tempPassword);

    const loginResult = await apiLogin(email, tempPassword);
    expect(loginResult.accessToken).toBeTruthy();
    expect(loginResult.user?.email || email).toBeTruthy();
  });
});

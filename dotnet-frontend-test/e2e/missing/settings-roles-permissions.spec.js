const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, apiRequest } = require("../helpers/api");

test.describe.configure({ mode: "serial" });

/** @param {import('@playwright/test').Page} page @param {string} roleName */
async function roleColumnIndex(page, roleName) {
  const headers = page.getByTestId("roles-permissions-panel").locator("thead th");
  const count = await headers.count();
  for (let i = 0; i < count; i += 1) {
    const text = (await headers.nth(i).innerText()).trim();
    if (text.split("\n")[0].trim() === roleName || text.includes(roleName)) {
      return i;
    }
  }
  return -1;
}

/** @param {import('@playwright/test').Page} page @param {string} featureLabel @param {number} colIndex */
function featureCheckbox(page, featureLabel, colIndex) {
  const row = page
    .getByTestId("roles-permissions-panel")
    .locator("tbody tr")
    .filter({ has: page.locator("td", { hasText: featureLabel }) })
    .first();
  return row.locator("td").nth(colIndex).getByRole("checkbox");
}

test.describe("Settings roles — create & permission toggles", () => {
  const stamp = Date.now();
  const roleName = `E2E_Role_${stamp}`;
  /** Use a built-in non-protected role that ListRoles always returns. */
  const toggleRole = "Viewer";
  const featureLabel = "Billing";

  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("create role succeeds via New Role dialog", async ({ page }) => {
    await page.goto("/settings/roles");
    await expect(page.getByTestId("roles-permissions-panel")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("new-role-btn").click();
    const dialog = page.getByRole("dialog", { name: /New Role/i });
    await expect(dialog).toBeVisible();
    await dialog.getByTestId("role-name-input").fill(roleName);
    await dialog.getByPlaceholder("Optional").fill("E2E created role");

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/admin/roles") && r.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: "Create" }).click();
    const res = await createResponse;
    expect(res.ok(), `Create role failed: ${res.status()} ${await res.text()}`).toBeTruthy();
    const body = await res.json();
    expect(body.name || body.Name).toBe(roleName);

    await expect(page.getByText(/Role created/i).first()).toBeVisible({ timeout: 10_000 });
    await expect(dialog).toBeHidden({ timeout: 10_000 });

    // Prove the role exists server-side (ListRoles omits unassigned custom roles).
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const updated = await apiRequest(admin.accessToken, "PUT", `/admin/roles/${encodeURIComponent(roleName)}`, {
      permissions: [],
      description: "E2E created role",
    });
    expect(updated?.name || updated?.Name).toBe(roleName);
  });

  test("toggle permissions save and persist after reload", async ({ page }) => {
    await page.goto("/settings/roles");
    await expect(page.getByTestId("roles-permissions-panel")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("roles-permissions-panel")).toContainText(toggleRole, {
      timeout: 15_000,
    });

    const col = await roleColumnIndex(page, toggleRole);
    expect(col, `Column for role ${toggleRole}`).toBeGreaterThan(0);

    const checkbox = featureCheckbox(page, featureLabel, col);
    await expect(checkbox).toBeVisible();
    const before = await checkbox.isChecked();
    await checkbox.click();
    await expect(checkbox).toBeChecked({ checked: !before });

    await expect(page.getByTestId("save-roles-btn")).toBeVisible();
    const saveResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/admin/roles/${encodeURIComponent(toggleRole)}`) &&
        r.request().method() === "PUT",
    );
    await page.getByTestId("save-roles-btn").click();
    const res = await saveResponse;
    expect(res.ok(), `Save role permissions failed: ${res.status()} ${await res.text()}`).toBeTruthy();

    await expect(page.getByTestId("save-roles-btn")).toHaveCount(0, { timeout: 15_000 });

    await page.reload({ waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("roles-permissions-panel")).toBeVisible({ timeout: 20_000 });

    const colAfter = await roleColumnIndex(page, toggleRole);
    expect(colAfter).toBeGreaterThan(0);
    const checkboxAfter = featureCheckbox(page, featureLabel, colAfter);
    await expect(checkboxAfter).toBeChecked({ checked: !before });

    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const roles = await apiRequest(admin.accessToken, "GET", "/admin/roles");
    const list = Array.isArray(roles) ? roles : roles?.items || [];
    const match = list.find((r) => r.name === toggleRole);
    expect(match, `${toggleRole} should exist`).toBeTruthy();
    const perms = match.permissions || [];
    const hasBilling = featureLabel === "Billing"
      ? ["Billing.View", "Billing.Create", "Billing.Edit"].every((p) => perms.includes(p))
      : perms.includes("Dashboard.View");
    expect(hasBilling).toBe(!before);

    // Restore prior state so the suite does not leave Viewer permanently changed.
    if ((await checkboxAfter.isChecked()) !== before) {
      await checkboxAfter.click();
      const restoreResponse = page.waitForResponse(
        (r) =>
          r.url().includes(`/api/admin/roles/${encodeURIComponent(toggleRole)}`) &&
          r.request().method() === "PUT",
      );
      await page.getByTestId("save-roles-btn").click();
      const restoreRes = await restoreResponse;
      expect(restoreRes.ok()).toBeTruthy();
    }
  });
});

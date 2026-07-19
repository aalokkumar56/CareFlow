const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, apiRequest } = require("../helpers/api");

/**
 * Settings templates CRUD — create + delete (UI has no edit dialog).
 */
test.describe.configure({ mode: "serial" });

test.describe("Templates CRUD", () => {
  const stamp = Date.now();
  const templateName = `E2E Template ${stamp}`;
  const templateBody = `Hi {name}, E2E template body ${stamp}`;

  let accessToken;
  let createdTemplateId;

  test.beforeAll(async () => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    accessToken = admin.accessToken;
  });

  test.afterAll(async () => {
    if (!accessToken || !createdTemplateId) return;
    try {
      await apiRequest(accessToken, "DELETE", `/templates/${createdTemplateId}`);
    } catch {
      /* already deleted by UI test */
    }
  });

  test("create, validate empty, and delete template", async ({ page }) => {
    await login(page);
    await page.goto("/settings/templates", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("new-tpl-btn")).toBeVisible({ timeout: 20_000 });

    // Empty create → toast, dialog stays open
    await page.getByTestId("new-tpl-btn").click();
    await expect(page.getByRole("dialog")).toBeVisible();
    await page.getByTestId("tpl-save-btn").click();
    await expect(page.getByText(/Name and body required/i).first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole("dialog")).toBeVisible();

    // Create
    await page.getByTestId("tpl-name").fill(templateName);
    await page.getByTestId("tpl-body").fill(templateBody);
    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/templates") && r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await page.getByTestId("tpl-save-btn").click();
    const res = await createResponse;
    expect(res.ok(), `Create template failed: ${res.status()} ${await res.text()}`).toBeTruthy();
    const created = await res.json();
    createdTemplateId = created.id;
    expect(createdTemplateId).toBeTruthy();

    await expect(page.getByText(templateName)).toBeVisible({ timeout: 15_000 });
    const row = page.getByTestId(`template-row-${createdTemplateId}`);
    await expect(row).toBeVisible();
    await expect(row).toContainText(`E2E template body ${stamp}`);

    // Delete
    const deleteResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/templates/${createdTemplateId}`) &&
        r.request().method() === "DELETE",
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: `Delete ${templateName}` }).click();
    expect((await deleteResponse).ok()).toBeTruthy();
    await expect(row).toHaveCount(0, { timeout: 15_000 });
    createdTemplateId = null;
  });
});

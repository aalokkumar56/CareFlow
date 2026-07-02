const { test, expect } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { clearBugLog } = require("./helpers/bug-log");

test.describe("Follow-ups kanban", () => {
  const taskTitle = `E2E Follow-up ${Date.now()}`;

  test.beforeAll(() => clearBugLog());

  test.beforeEach(async ({ page }) => {
    await login(page);
    await page.goto("/tasks", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("followups-kanban")).toBeVisible({ timeout: 20_000 });
  });

  test("create follow-up and complete moves card to Completed column", async ({ page }) => {
    await page.getByTestId("new-task-btn").click();
    await page.getByTestId("task-title").fill(taskTitle);

    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
      .toISOString()
      .slice(0, 16);
    await page.locator('input[type="datetime-local"]').fill(local);

    const createResponse = page.waitForResponse(
      (r) => r.url().includes("/api/tasks") && r.request().method() === "POST" && r.ok(),
      { timeout: 15_000 },
    );
    await page.getByTestId("task-save-btn").click();
    await createResponse;

    await expect(page.getByText(taskTitle)).toBeVisible({ timeout: 15_000 });

    const card = page.locator("[data-testid^='followup-card-']", { hasText: taskTitle });
    const completeBtn = card.getByRole("button", { name: "Complete" });

    const patchResponse = page.waitForResponse(
      (r) => r.url().includes("/api/tasks/") && r.request().method() === "PATCH" && r.ok(),
      { timeout: 15_000 },
    );
    await completeBtn.click();
    await patchResponse;

    const completedCol = page.getByTestId("followups-col-completed");
    await expect(completedCol.getByText(taskTitle)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole("button", { name: "Reopen" }).first()).toBeVisible();
  });
});

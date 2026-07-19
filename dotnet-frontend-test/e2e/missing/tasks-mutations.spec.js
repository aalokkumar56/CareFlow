const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, createTask } = require("../helpers/api");

function localDatetimeOffset(days) {
  const d = new Date();
  d.setDate(d.getDate() + days);
  const local = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 16);
}

async function ensureAdminToken(retries = 5) {
  let lastErr;
  for (let i = 0; i < retries; i += 1) {
    try {
      return (await apiLogin("admin@cureflow.in", "admin123")).accessToken;
    } catch (err) {
      lastErr = err;
      await new Promise((r) => setTimeout(r, 2000 * (i + 1)));
    }
  }
  throw lastErr;
}

test.describe("Tasks mutations (UI-HIGH-039…044)", () => {
  const stamp = Date.now();

  test("UI-HIGH-039: drag follow-up onto Completed marks done via API", async ({ page }) => {
    // Board columns are time-based; the only drag mutation is drop on Completed → status done.
    const adminToken = await ensureAdminToken();
    const title = `E2E Drag Task ${stamp}`;
    const dueAt = new Date();
    dueAt.setHours(dueAt.getHours() + 3);
    const created = await createTask(adminToken, { title, dueAt: dueAt.toISOString() });
    const taskId = created.id || created.task?.id;
    expect(taskId).toBeTruthy();

    await login(page);
    await page.goto("/tasks", { waitUntil: "networkidle" });
    await expect(page.getByTestId("followups-kanban")).toBeVisible({ timeout: 20_000 });

    const card = page.getByTestId(`followup-card-${taskId}`);
    await expect(card).toBeVisible({ timeout: 15_000 });

    const completedCol = page.getByTestId("followups-col-completed");
    await expect(completedCol).toBeVisible();

    const patchPromise = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/tasks/${taskId}`)
        && r.request().method() === "PATCH"
        && r.ok(),
      { timeout: 15_000 },
    );

    await card.dragTo(completedCol);
    await patchPromise;

    await expect(completedCol.getByText(title)).toBeVisible({ timeout: 15_000 });
  });

  test("UI-HIGH-040: mark overdue task complete", async ({ page }) => {
    const adminToken = await ensureAdminToken();
    const title = `E2E Overdue Task ${stamp}`;
    const dueAt = new Date();
    dueAt.setDate(dueAt.getDate() - 3);
    const created = await createTask(adminToken, { title, dueAt: dueAt.toISOString() });
    const taskId = created.id || created.task?.id;

    await login(page);
    await page.goto("/tasks", { waitUntil: "networkidle" });
    await expect(page.getByTestId(`followup-card-${taskId}`)).toBeVisible({ timeout: 15_000 });

    const patchPromise = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/tasks/${taskId}`)
        && r.request().method() === "PATCH"
        && r.ok(),
    );
    await page.getByTestId(`followup-complete-${taskId}`).click();
    await patchPromise;

    await expect(page.getByTestId("followups-col-completed").getByText(title)).toBeVisible({
      timeout: 15_000,
    });
  });

  test("UI-HIGH-041: empty state New Task button creates card", async ({ page }) => {
    const title = `E2E Empty Create ${stamp}`;
    await login(page);
    await page.goto("/tasks", { waitUntil: "networkidle" });

    await page.getByTestId("task-search").fill(`__no_match_${stamp}__`);
    await expect(page.getByTestId("tasks-empty-state")).toBeVisible({ timeout: 15_000 });

    await page.getByTestId("empty-new-task-btn").click();
    await expect(page.getByTestId("task-title")).toBeVisible();
    await page.getByTestId("task-title").fill(title);
    await page.locator('input[type="datetime-local"]').fill(localDatetimeOffset(0));

    const createPromise = page.waitForResponse(
      (r) => r.url().includes("/api/tasks") && r.request().method() === "POST" && r.ok(),
    );
    await page.getByTestId("task-save-btn").click();
    await createPromise;

    await page.getByTestId("task-search").fill("");
    await expect(page.getByText(title)).toBeVisible({ timeout: 15_000 });
  });

  test("UI-HIGH-043: delete task removes card", async ({ page }) => {
    const adminToken = await ensureAdminToken();
    const title = `E2E Delete Task ${stamp}`;
    const dueAt = new Date();
    dueAt.setHours(dueAt.getHours() + 4);
    const created = await createTask(adminToken, { title, dueAt: dueAt.toISOString() });
    const taskId = created.id || created.task?.id;

    await login(page);
    await page.goto("/tasks", { waitUntil: "networkidle" });
    const card = page.getByTestId(`followup-card-${taskId}`);
    await expect(card).toBeVisible({ timeout: 15_000 });

    page.once("dialog", async (d) => { await d.accept(); });

    const deletePromise = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/tasks/${taskId}`)
        && r.request().method() === "DELETE",
    );
    await card.getByRole("button", { name: /delete follow-up/i }).click();
    const res = await deletePromise;
    expect(res.ok() || res.status() === 204).toBeTruthy();

    await expect(page.getByTestId(`followup-card-${taskId}`)).toHaveCount(0, { timeout: 15_000 });
  });

  test("UI-HIGH-044: task search filters board by title", async ({ page }) => {
    const adminToken = await ensureAdminToken();
    const title = `E2E Searchable Task ${stamp}`;
    const dueAt = new Date();
    dueAt.setHours(dueAt.getHours() + 5);
    await createTask(adminToken, { title, dueAt: dueAt.toISOString() });

    await login(page);
    await page.goto("/tasks", { waitUntil: "networkidle" });
    await page.getByTestId("task-search").fill(title);
    await expect(page.getByText(title)).toBeVisible({ timeout: 15_000 });

    await page.getByTestId("task-search").fill(`__missing_${stamp}__`);
    await expect(page.getByText(title)).toHaveCount(0, { timeout: 10_000 });
  });
});

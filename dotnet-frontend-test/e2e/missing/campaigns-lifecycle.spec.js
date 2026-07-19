const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, apiRequest } = require("../helpers/api");

test.describe.configure({ mode: "serial" });

/** Local `datetime-local` value N hours ahead. */
function localDateTimeOffset(hoursAhead = 26) {
  const d = new Date(Date.now() + hoursAhead * 3600_000);
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/**
 * Create a draft campaign via the UI and return its id.
 * @param {import('@playwright/test').Page} page
 * @param {string} name
 * @param {{ tags?: string }} [opts]
 */
async function createDraftViaUi(page, name, opts = {}) {
  await page.goto("/campaigns", { waitUntil: "domcontentloaded" });
  await expect(page.getByTestId("campaigns-kanban")).toBeVisible({ timeout: 20_000 });

  await page.getByTestId("new-campaign-btn").click();
  await expect(page.getByTestId("new-campaign-dialog")).toBeVisible();
  await page.getByTestId("nc-name").fill(name);
  await page.getByTestId("nc-message").fill(`Hello from ${name}`);
  if (opts.tags) {
    await page.getByTestId("nc-tags").fill(opts.tags);
  }

  const createResponse = page.waitForResponse(
    (r) =>
      r.url().includes("/api/campaigns") &&
      r.request().method() === "POST" &&
      !r.url().includes("preview-audience"),
    { timeout: 20_000 },
  );
  await page.getByTestId("nc-save-btn").click();
  const res = await createResponse;
  expect(res.ok(), `Create campaign failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  const body = await res.json();
  expect(body.id).toBeTruthy();

  await expect(page.getByTestId("new-campaign-dialog")).toBeHidden({ timeout: 15_000 });
  await expect(page.getByTestId("campaigns-kanban")).toContainText(name, { timeout: 15_000 });
  return body.id;
}

test.describe("Campaigns lifecycle — preview, schedule, cancel, send", () => {
  const stamp = Date.now();
  let token;

  test.beforeAll(async () => {
    const session = await apiLogin("admin@cureflow.in", "admin123");
    token = session.accessToken;
  });

  test("audience preview returns a count in the create dialog", async ({ page }) => {
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto("/campaigns", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("campaigns-kanban")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("new-campaign-btn").click();
    await expect(page.getByTestId("new-campaign-dialog")).toBeVisible();

    const previewResponse = page.waitForResponse(
      (r) =>
        r.url().includes("/api/campaigns/preview-audience") &&
        r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await page.getByTestId("nc-preview-btn").click();
    const res = await previewResponse;
    expect(res.ok(), `Preview failed: ${res.status()} ${await res.text()}`).toBeTruthy();
    const body = await res.json();
    expect(typeof body.count).toBe("number");
    expect(body.count).toBeGreaterThanOrEqual(0);

    await expect(page.getByTestId("new-campaign-dialog")).toContainText(
      /patient(s)? match this audience/i,
      { timeout: 10_000 },
    );
  });

  test("schedule auto-send then cancel returns campaign to draft", async ({ page }) => {
    const name = `E2E Schedule ${stamp}`;
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    const campaignId = await createDraftViaUi(page, name);

    await page.locator(`a[href="/campaigns/${campaignId}"]`).click();
    await page.waitForURL(new RegExp(`/campaigns/${campaignId}`), { timeout: 20_000 });
    await expect(page.getByTestId("schedule-campaign-btn")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("schedule-campaign-btn").click();
    await expect(page.getByTestId("schedule-dialog")).toBeVisible();
    await page.getByTestId("schedule-datetime").fill(localDateTimeOffset(30));

    const scheduleResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/campaigns/${campaignId}/schedule`) &&
        r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: /Enable auto-send/i }).click();
    expect((await scheduleResponse).ok()).toBeTruthy();

    await expect(page.getByTestId("cancel-schedule-btn")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/Scheduled for automatic delivery/i)).toBeVisible();

    const cancelResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/campaigns/${campaignId}`) &&
        r.request().method() === "PATCH",
      { timeout: 20_000 },
    );
    await page.getByTestId("cancel-schedule-btn").click();
    expect((await cancelResponse).ok()).toBeTruthy();

    await expect(page.getByTestId("schedule-campaign-btn")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId("send-campaign-btn")).toBeVisible();

    const detail = await apiRequest(token, "GET", `/campaigns/${campaignId}`);
    const status = (detail?.campaign?.status || "").toLowerCase();
    expect(status).toBe("draft");
  });

  test("send now confirms and marks campaign sent", async ({ page }) => {
    test.setTimeout(180_000);
    const name = `E2E Send ${stamp}`;
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    // Unique tag keeps audience tiny so WhatsApp batch send finishes in CI/dev.
    const campaignId = await createDraftViaUi(page, name, {
      tags: `e2e-send-${stamp}`,
    });

    await page.locator(`a[href="/campaigns/${campaignId}"]`).click();
    await page.waitForURL(new RegExp(`/campaigns/${campaignId}`), { timeout: 20_000 });
    await expect(page.getByTestId("send-campaign-btn")).toBeVisible({ timeout: 20_000 });

    await page.getByTestId("send-campaign-btn").click();
    await expect(page.getByTestId("confirm-send-dialog")).toBeVisible();

    const sendResponsePromise = page.waitForResponse(
      (r) =>
        /\/api\/campaigns\/[^/]+\/send/i.test(r.url()) &&
        r.request().method() === "POST",
      { timeout: 90_000 },
    );
    await page.getByRole("button", { name: /Yes, send now/i }).click();
    const res = await sendResponsePromise;
    expect(res.ok(), `Send failed: ${res.status()} ${await res.text()}`).toBeTruthy();
    const payload = await res.json();
    expect(typeof payload.sent).toBe("number");
    expect(typeof payload.failed).toBe("number");

    await expect(page.getByTestId("send-campaign-btn")).toHaveCount(0);
    await expect(page.getByTestId("schedule-campaign-btn")).toHaveCount(0);

    await expect
      .poll(async () => {
        const detail = await apiRequest(token, "GET", `/campaigns/${campaignId}`);
        return (detail?.campaign?.status || "").toLowerCase();
      }, { timeout: 30_000 })
      .toBe("sent");
  });
});

// STRICT: asserts expected success OR expected failure; no soft-pass
/**
 * Critical WhatsApp compose / send / permission mutations.
 *
 * Scenario IDs (TEST_SCENARIOS.md):
 *   UI-CRIT-001 … UI-CRIT-010
 */
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, apiRequest, createPatient, apiURL } = require("../helpers/api");
const { ensureTestUsers, E2E_PASSWORD } = require("../helpers/test-users");
const { E2E_TEST_PHONE } = require("../helpers/constants");
const {
  PERMISSIONS,
  getPermissionsForUser,
} = require("../../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

function patientIdFrom(created) {
  return created?.id || created?.patient?.id;
}

function conversationIdFrom(payload) {
  return (
    payload?.conversation?.id ||
    payload?.conversation?.Id ||
    payload?.id ||
    null
  );
}

/** @param {import('@playwright/test').Page} page */
function composeInput(page) {
  return page
    .getByPlaceholder("Type a WhatsApp message...")
    .or(page.getByPlaceholder("WhatsApp sending is disabled"))
    .or(page.getByPlaceholder(/Add a caption/i));
}

/** @param {import('@playwright/test').Page} page */
function sendButton(page) {
  return page.getByRole("button", { name: "Send message" });
}

/**
 * @param {import('@playwright/test').Page} page
 * @param {string} text
 */
async function sendWhatsAppText(page, text) {
  const input = page.getByPlaceholder("Type a WhatsApp message...");
  await expect(input, "compose must be visible and enabled to send").toBeVisible({ timeout: 15_000 });
  await expect(input).toBeEnabled();
  await input.fill(text);
  const btn = sendButton(page);
  await expect(btn).toBeEnabled({ timeout: 5_000 });

  const responsePromise = page.waitForResponse(
    (r) =>
      r.url().includes("/api/conversations/messages") &&
      r.request().method() === "POST",
    { timeout: 25_000 },
  );
  await btn.click();
  const res = await responsePromise;
  expect(res.ok(), `Send failed: HTTP ${res.status()}`).toBeTruthy();
}

async function getWhatsAppStatus(token) {
  const res = await fetch(`${apiURL}/api/settings/whatsapp/status`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) return { enabled: false, is_configured: false };
  return res.json();
}

test.describe("WhatsApp mutations — send, compose, permissions", () => {
  const stamp = Date.now();
  /** @type {Record<string, { email: string, password: string }>} */
  let users = {};
  let adminToken;
  let patientId;
  let patientName;
  let conversationId;
  let patientPhone;
  /** @type {{ enabled?: boolean, is_configured?: boolean, message?: string }} */
  let waStatus = {};

  test.beforeAll(async () => {
    users = await ensureTestUsers(stamp);
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    adminToken = admin.accessToken;
    waStatus = await getWhatsAppStatus(adminToken);

    patientName = `E2E WA Patient ${stamp}`;
    patientPhone = `91${String(stamp).slice(-8)}`;
    const created = await createPatient(adminToken, patientName, patientPhone);
    patientId = patientIdFrom(created);
    expect(patientId, "patient id").toBeTruthy();

    const convPayload = await apiRequest(
      adminToken,
      "GET",
      `/conversations/patient/${patientId}`,
    );
    conversationId = conversationIdFrom(convPayload);
    expect(conversationId, "conversation id").toBeTruthy();

    await apiRequest(adminToken, "POST", "/conversations/messages", {
      conversation_id: conversationId,
      body: `Seed message ${stamp}`,
    });
  });

  test("UI-CRIT-001: admin opens conversation, sends text, message appears", async ({ page }) => {
    const body = `E2E inbox send ${stamp}`;
    const waReady = waStatus.enabled && waStatus.is_configured;

    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("inbox-search")).toBeVisible({ timeout: 20_000 });

    const convBtn = page.getByTestId(`conv-${conversationId}`);
    await expect(convBtn).toBeVisible({ timeout: 20_000 });
    await convBtn.click();
    await expect(composeInput(page)).toBeVisible({ timeout: 15_000 });

    if (!waReady) {
      await expect(page.getByPlaceholder("WhatsApp sending is disabled")).toBeDisabled();
      await expect(sendButton(page)).toBeDisabled();
      return;
    }

    await expect(page.getByPlaceholder("Type a WhatsApp message...")).toBeEnabled({ timeout: 15_000 });
    await sendWhatsAppText(page, body);
    await expect(page.getByText(body).first()).toBeVisible({ timeout: 15_000 });
  });

  test("UI-CRIT-002: reception sends WhatsApp from patient detail panel", async ({ page }) => {
    const body = `E2E reception send ${stamp}`;
    const receptionSession = await apiLogin(
      users.reception.email,
      users.reception.password || E2E_PASSWORD,
    );
    const receptionWa = await getWhatsAppStatus(receptionSession.accessToken);
    const waReady = !!(receptionWa.enabled && receptionWa.is_configured);

    await login(page, {
      email: users.reception.email,
      password: users.reception.password || E2E_PASSWORD,
    });
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("patient-whatsapp-icon")).toBeVisible();
    await page.getByTestId("patient-whatsapp-icon").click();
    await expect(page.getByRole("heading", { name: "WhatsApp" })).toBeVisible({ timeout: 10_000 });
    await expect(composeInput(page)).toBeVisible({ timeout: 15_000 });

    if (!waReady) {
      await expect(page.getByPlaceholder("WhatsApp sending is disabled")).toBeDisabled({
        timeout: 15_000,
      });
      await expect(sendButton(page)).toBeDisabled();
      return;
    }

    await expect(page.getByPlaceholder("Type a WhatsApp message...")).toBeEnabled({ timeout: 15_000 });
    await sendWhatsAppText(page, body);
    await expect(page.getByText(body).first()).toBeVisible({ timeout: 15_000 });
  });

  test("UI-CRIT-003: nurse cannot send WhatsApp (compose/send hidden)", async ({ page }) => {
    await login(page, {
      email: users.nurse.email,
      password: users.nurse.password || E2E_PASSWORD,
    });
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("patient-whatsapp-icon")).toHaveCount(0);

    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(1_000);
    const onInbox = page.url().includes("/inbox");
    if (onInbox) {
      await expect(page.getByTestId("inbox-search")).toHaveCount(0);
    } else {
      await expect(page).not.toHaveURL(/\/inbox/);
    }
  });

  test("UI-CRIT-004: admin attach file and send", async ({ page }) => {
    const waReady = waStatus.enabled && waStatus.is_configured;
    // 1x1 PNG — explicit mime so outbound policy accepts the upload.
    const pngBuffer = Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
      "base64",
    );

    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
    await page.getByTestId("patient-whatsapp-icon").click();
    await expect(page.getByRole("heading", { name: "WhatsApp" })).toBeVisible({ timeout: 10_000 });

    const attachBtn = page.getByRole("button", { name: "Attach file" });
    await expect(attachBtn).toBeVisible();

    if (!waReady) {
      await expect(attachBtn).toBeDisabled({ timeout: 15_000 });
      return;
    }

    await expect(attachBtn).toBeEnabled({ timeout: 15_000 });
    const fileChooserPromise = page.waitForEvent("filechooser");
    await attachBtn.click();
    const chooser = await fileChooserPromise;
    await chooser.setFiles({
      name: "e2e-wa-attach.png",
      mimeType: "image/png",
      buffer: pngBuffer,
    });
    await expect(page.getByText("e2e-wa-attach.png")).toBeVisible({ timeout: 10_000 });

    const mediaResponse = page.waitForResponse(
      (r) =>
        r.url().includes(`/api/conversations/${conversationId}/media`) &&
        r.request().method() === "POST",
      { timeout: 30_000 },
    );
    await sendButton(page).click();
    const res = await mediaResponse;
    expect(res.ok(), `Media send failed: HTTP ${res.status()} ${await res.text()}`).toBeTruthy();
    await expect(page.getByText(/Attachment sent/i).first()).toBeVisible({ timeout: 15_000 });
    // Image outbound renders as media (img/preview), not always the raw filename string.
    await expect(
      page.locator("img[alt], video, a[download], img").first(),
    ).toBeVisible({ timeout: 15_000 });
  });

  test("UI-CRIT-005: marketing WhatsApp.Send without Conversation.View is denied load", async ({ page }) => {
    const session = await apiLogin(users.marketing.email, users.marketing.password || E2E_PASSWORD);
    const perms = getPermissionsForUser(session.user);
    expect(perms.includes(PERMISSIONS.WhatsAppSend), "marketing fixture must have WhatsApp.Send").toBeTruthy();
    expect(
      perms.includes(PERMISSIONS.ConversationView),
      "marketing fixture must lack Conversation.View for this deny path",
    ).toBeFalsy();

    await login(page, {
      email: users.marketing.email,
      password: users.marketing.password || E2E_PASSWORD,
    });
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("patient-whatsapp-icon")).toBeVisible();
    await page.getByTestId("patient-whatsapp-icon").click();
    await expect(page.getByRole("heading", { name: "WhatsApp" })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText(/Failed to load WhatsApp conversation/i).first()).toBeVisible({
      timeout: 15_000,
    });
  });

  test("UI-CRIT-006: staff WhatsApp send hidden when WhatsApp.Send not granted", async ({ page }) => {
    await login(page, {
      email: users.staff.email,
      password: users.staff.password || E2E_PASSWORD,
    });
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId("patient-whatsapp-icon")).toHaveCount(0);
  });

  test("UI-CRIT-007: admin opening conversation clears unread badge", async ({ page }) => {
    const inbound = `E2E unread ${stamp}`;
    const demoUrl = `${apiURL}/api/whatsapp/demo/inbound`;
    const demoRes = await fetch(demoUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        message: inbound,
        phone: patientPhone || E2E_TEST_PHONE,
      }),
    });
    expect(demoRes && typeof demoRes.ok === "boolean", `Unexpected fetch result from ${demoUrl}`).toBeTruthy();
    expect(demoRes.ok, `Demo inbound must be available: HTTP ${demoRes.status}`).toBeTruthy();

    await new Promise((r) => setTimeout(r, 2_000));

    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("inbox-search")).toBeVisible({ timeout: 20_000 });

    const convBtn = page.getByTestId(`conv-${conversationId}`);
    await expect(convBtn).toBeVisible({ timeout: 20_000 });
    await convBtn.click();
    await expect(composeInput(page)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(inbound).first()).toBeVisible({ timeout: 15_000 });

    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId(`conv-${conversationId}`)).toBeVisible({ timeout: 20_000 });
    const badge = page.getByTestId(`conv-${conversationId}`).locator(".badge-count");
    await expect(badge).toHaveCount(0);
  });

  test("UI-CRIT-008: admin empty compose cannot send", async ({ page }) => {
    const waReady = waStatus.enabled && waStatus.is_configured;

    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 20_000 });
    await page.getByTestId("patient-whatsapp-icon").click();
    await expect(page.getByRole("heading", { name: "WhatsApp" })).toBeVisible({ timeout: 10_000 });

    if (!waReady) {
      await expect(sendButton(page)).toBeDisabled();
      return;
    }

    const input = page.getByPlaceholder("Type a WhatsApp message...");
    await expect(input).toBeVisible({ timeout: 15_000 });
    await input.fill("");
    await expect(sendButton(page)).toBeDisabled();
  });

  test("UI-CRIT-009: send path fails gracefully when WhatsApp not configured", async ({ page }) => {
    await login(page, { email: "admin@cureflow.in", password: "admin123" });

    const statusRes = await page.request.get(`${apiURL}/api/settings/whatsapp/status`, {
      headers: { Authorization: `Bearer ${adminToken}` },
    });
    expect(statusRes.ok()).toBeTruthy();
    const status = await statusRes.json();
    const disabled = !status.enabled || !status.is_configured;

    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("inbox-search")).toBeVisible({ timeout: 20_000 });

    const convBtn = page.getByTestId(`conv-${conversationId}`);
    await expect(convBtn).toBeVisible({ timeout: 20_000 });
    await convBtn.click();

    if (disabled) {
      await expect(
        page.getByText(/WhatsApp messaging is disabled|not configured|Settings → Integrations/i),
      ).toBeVisible({ timeout: 10_000 });
      await expect(page.getByPlaceholder("WhatsApp sending is disabled")).toBeDisabled({
        timeout: 15_000,
      });
      await expect(sendButton(page)).toBeDisabled();
    } else {
      await expect(page.getByPlaceholder("Type a WhatsApp message...")).toBeEnabled({
        timeout: 15_000,
      });
    }
  });

  test("UI-CRIT-010: admin conversation search opens matching thread history", async ({ page }) => {
    await login(page, { email: "admin@cureflow.in", password: "admin123" });
    await page.goto("/inbox", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("inbox-search")).toBeVisible({ timeout: 20_000 });

    const listResponse = page.waitForResponse(
      (r) =>
        r.url().includes("/api/conversations") &&
        r.request().method() === "GET" &&
        r.url().includes("q="),
      { timeout: 20_000 },
    );
    await page.getByTestId("inbox-search").fill(patientName);
    await listResponse;

    const convBtn = page.getByTestId(`conv-${conversationId}`);
    await expect(convBtn).toBeVisible({ timeout: 20_000 });
    await convBtn.click();

    await expect(composeInput(page)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(`Seed message ${stamp}`).first()).toBeVisible({
      timeout: 15_000,
    });
  });
});

// STRICT: asserts expected success OR expected failure; no soft-pass
const net = require("net");
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, apiRequest } = require("../helpers/api");
const { E2E_TEST_PHONE } = require("../helpers/constants");

/**
 * Email inbox mutations — compose/send, disabled state, search.
 * Seeds a local SMTP sink so enabled-send path returns HTTP 200.
 */
test.describe.configure({ mode: "serial" });

function startSmtpSink(port = 2525) {
  const server = net.createServer((socket) => {
    socket.write("220 localhost ESMTP e2e\r\n");
    let inData = false;
    let buffer = "";
    socket.on("error", () => {
      /* client disconnects are expected */
    });
    socket.on("data", (buf) => {
      buffer += buf.toString("utf8");
      let idx;
      while ((idx = buffer.indexOf("\r\n")) >= 0) {
        const line = buffer.slice(0, idx);
        buffer = buffer.slice(idx + 2);
        if (inData) {
          if (line === ".") {
            inData = false;
            socket.write("250 OK\r\n");
          }
          continue;
        }
        const cmd = line.split(" ")[0].toUpperCase();
        if (cmd === "DATA") {
          inData = true;
          socket.write("354 End data with <CR><LF>.<CR><LF>\r\n");
        } else if (cmd === "QUIT") {
          socket.write("221 Bye\r\n");
          socket.end();
        } else if (cmd === "EHLO" || cmd === "HELO") {
          socket.write("250-localhost\r\n250 AUTH PLAIN\r\n");
        } else {
          socket.write("250 OK\r\n");
        }
      }
    });
  });
  server.on("error", () => {
    /* ignore accept-time errors */
  });
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, "127.0.0.1", () => resolve(server));
  });
}

test.describe("Email inbox mutations", () => {
  const stamp = Date.now();
  const uniqueSubject = `E2E Email Subject ${stamp}`;
  const uniqueBody = `E2E email body seed ${stamp}`;
  const replyBody = `E2E email reply ${stamp}`;
  const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";
  const smtpPort = 2525;

  let accessToken;
  let patientId;
  let patientName;
  let originalEmailSettings;
  /** @type {import('net').Server | null} */
  let smtpServer = null;

  async function saveEmailSettings(token, patch) {
    return apiRequest(token, "POST", "/settings/email", {
      smtp_host: patch.smtpHost ?? null,
      smtp_port: patch.smtpPort ?? 587,
      smtp_username: patch.smtpUsername ?? null,
      smtp_password: patch.smtpPassword ?? null,
      use_ssl: patch.useSsl !== false,
      from_email: patch.fromEmail ?? null,
      from_name: patch.fromName ?? null,
      enabled: !!patch.enabled,
      send_with_whatsapp: patch.sendWithWhatsApp !== false,
    });
  }

  async function seedOutboundEmail(token, patient, subject, body) {
    const res = await fetch(`${apiURL}/api/email/send`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ patient_id: patient, subject, body }),
    });
    const text = await res.text();
    return { status: res.status, text };
  }

  test.beforeAll(async () => {
    smtpServer = await startSmtpSink(smtpPort);

    const admin = await apiLogin("admin@cureflow.in", "admin123");
    accessToken = admin.accessToken;
    originalEmailSettings = await apiRequest(accessToken, "GET", "/settings/email");

    patientName = `E2E Email Patient ${stamp}`;
    const patient = await apiRequest(accessToken, "POST", "/patients", {
      name: patientName,
      phone: E2E_TEST_PHONE,
      email: `e2e.email.${stamp}@cureflow.test`,
      inquiry_source: "e2e",
    });
    patientId = patient.id || patient.patient?.id;
    expect(patientId, "Need patient with email for inbox tests").toBeTruthy();

    await apiRequest(accessToken, "PATCH", `/patients/${patientId}`, {
      email_notifications_enabled: true,
      email: `e2e.email.${stamp}@cureflow.test`,
    });

    await saveEmailSettings(accessToken, {
      smtpHost: "127.0.0.1",
      smtpPort,
      fromEmail: "noreply@cureflow.test",
      fromName: "CureFlow E2E",
      enabled: true,
      useSsl: false,
    });

    const saved = await apiRequest(accessToken, "GET", "/settings/email");
    expect(saved?.smtp_host, `SMTP host not saved: ${JSON.stringify(saved)}`).toBeTruthy();
    expect(saved?.from_email, `From email not saved: ${JSON.stringify(saved)}`).toBeTruthy();
    expect(saved?.enabled, `Email not enabled: ${JSON.stringify(saved)}`).toBeTruthy();

    const seeded = await seedOutboundEmail(accessToken, patientId, uniqueSubject, uniqueBody);
    expect(seeded.status, `Email seed must succeed with SMTP sink: ${seeded.text}`).toBe(200);

    const threads = await apiRequest(
      accessToken,
      "GET",
      `/email/threads?q=${encodeURIComponent(String(stamp))}`,
    );
    const list = threads?.threads || [];
    expect(
      list.some((t) => String(t.patient_id || t.patientId) === String(patientId)),
      `Seeded email thread missing. threads=${JSON.stringify(list).slice(0, 400)}`,
    ).toBeTruthy();
  });

  test.afterAll(async () => {
    if (accessToken && originalEmailSettings) {
      try {
        await saveEmailSettings(accessToken, {
          smtpHost: originalEmailSettings.smtp_host ?? null,
          smtpPort: originalEmailSettings.smtp_port ?? 587,
          smtpUsername: originalEmailSettings.smtp_username ?? null,
          fromEmail: originalEmailSettings.from_email ?? null,
          fromName: originalEmailSettings.from_name ?? null,
          enabled: !!originalEmailSettings.enabled,
          useSsl: originalEmailSettings.use_ssl !== false,
          sendWithWhatsApp: originalEmailSettings.send_with_whatsapp !== false,
        });
      } catch {
        /* best-effort restore */
      }
    }
    if (smtpServer) {
      await new Promise((resolve) => {
        smtpServer.close(() => resolve());
        setTimeout(resolve, 1000);
      });
      smtpServer = null;
    }
  });

  test("disabled compose when SMTP off; search + reply 200 when SMTP on", async ({ page }) => {
    expect(patientId, "patient seeded in beforeAll").toBeTruthy();

    // --- SMTP off → disabled UI ---
    await saveEmailSettings(accessToken, {
      smtpHost: "127.0.0.1",
      smtpPort,
      fromEmail: "noreply@cureflow.test",
      fromName: "CureFlow E2E",
      enabled: false,
      useSsl: false,
    });

    await login(page);
    await page.goto("/email-inbox", { waitUntil: "domcontentloaded" });

    const banner = page.getByText(/email is disabled|not configured|enable it under settings/i);
    await expect(banner.first()).toBeVisible({ timeout: 20_000 });

    const search = page.getByPlaceholder("Search email threads...");
    await search.fill(String(stamp));
    await page.waitForTimeout(500);
    const threadBtn = page.getByRole("button").filter({ hasText: patientName }).first();
    await expect(threadBtn).toBeVisible({ timeout: 15_000 });
    await threadBtn.click();
    await expect(page.getByPlaceholder("Subject")).toBeDisabled({ timeout: 10_000 });
    await expect(
      page.getByPlaceholder(/Compose email|Email sending is disabled/),
    ).toBeDisabled();
    await expect(page.getByRole("button", { name: /send email/i })).toBeDisabled();

    // --- SMTP on → search + reply must return 200 ---
    await saveEmailSettings(accessToken, {
      smtpHost: "127.0.0.1",
      smtpPort,
      fromEmail: "noreply@cureflow.test",
      fromName: "CureFlow E2E",
      enabled: true,
      useSsl: false,
    });
    await page.reload({ waitUntil: "domcontentloaded" });
    await expect(search).toBeVisible({ timeout: 20_000 });

    const threadsResponse = page.waitForResponse(
      (r) =>
        r.url().includes("/api/email/threads") &&
        r.url().includes("q=") &&
        r.request().method() === "GET",
      { timeout: 20_000 },
    );
    await search.fill(uniqueSubject.slice(0, 20));
    await threadsResponse;
    await expect(page.getByRole("button").filter({ hasText: patientName }).first()).toBeVisible({
      timeout: 15_000,
    });

    await search.fill(`zzz-no-match-${stamp}`);
    await page.waitForTimeout(700);
    await expect(page.getByText(/No email threads yet/i)).toBeVisible({ timeout: 15_000 });

    await search.fill(String(stamp));
    await page.waitForTimeout(500);
    await page.getByRole("button").filter({ hasText: patientName }).first().click();

    const subject = page.getByPlaceholder("Subject");
    const body = page.getByPlaceholder(/Compose email/);
    await expect(subject).toBeEnabled({ timeout: 10_000 });
    await expect(body).toBeEnabled();
    await subject.fill(`Reply ${stamp}`);
    await body.fill(replyBody);

    const sendResponse = page.waitForResponse(
      (r) => r.url().includes("/api/email/send") && r.request().method() === "POST",
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: /send email/i }).click();
    const res = await sendResponse;
    expect(res.status(), `Send with SMTP on must be 200: ${await res.text()}`).toBe(200);

    await page.reload({ waitUntil: "domcontentloaded" });
    await page.getByPlaceholder("Search email threads...").fill(String(stamp));
    await page.waitForTimeout(500);
    await page.getByRole("button").filter({ hasText: patientName }).first().click();
    await expect(page.getByText(replyBody).first()).toBeVisible({ timeout: 15_000 });
  });
});

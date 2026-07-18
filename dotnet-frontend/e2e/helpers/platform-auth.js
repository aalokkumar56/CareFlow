const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";

/**
 * Signs in as the platform ops user. On a fresh installation (no platform users
 * yet) the /platform/login page shows the first-time setup form, so we bootstrap
 * the owner instead of logging in. This keeps platform E2E tests working whether
 * or not the ops account was seeded.
 *
 * @param {import('@playwright/test').Page} page
 */
async function loginAsPlatformOps(page, {
  name = "CureFlow Ops",
  email = "ops@cureflow.in",
  password = "OpsAdmin123!",
} = {}) {
  await page.goto("/platform/login", { waitUntil: "domcontentloaded" });

  // The setup form only renders when the backend reports needs_setup=true.
  const needsSetup = await page
    .getByTestId("platform-bootstrap-form")
    .isVisible()
    .catch(() => false);

  const authResponse = page.waitForResponse(
    (r) =>
      /\/api\/platform\/auth\/(login|bootstrap)$/.test(r.url()) &&
      r.request().method() === "POST",
    { timeout: 20_000 },
  );

  if (needsSetup) {
    await page.getByTestId("platform-bootstrap-name").fill(name);
    await page.getByTestId("platform-bootstrap-email").fill(email);
    await page.getByTestId("platform-bootstrap-password").fill(password);
    await page.getByTestId("platform-bootstrap-confirm").fill(password);
    await page.getByTestId("platform-bootstrap-submit").click();
  } else {
    await page.getByTestId("platform-login-email").fill(email);
    await page.getByTestId("platform-login-password").fill(password);
    await page.getByTestId("platform-login-submit").click();
  }

  const res = await authResponse;
  if (!res.ok()) {
    throw new Error(`Platform auth failed: ${res.status()} ${await res.text()}`);
  }

  const data = await res.json();
  const token = data.access_token || data.accessToken;
  await page.evaluate((tokenValue) => {
    localStorage.setItem("cureflow_platform_token", tokenValue);
  }, token);

  await page.goto("/platform/tenants", { waitUntil: "domcontentloaded" });
  await page.getByTestId("platform-shell").waitFor({ state: "visible", timeout: 15_000 });
}

module.exports = { loginAsPlatformOps };

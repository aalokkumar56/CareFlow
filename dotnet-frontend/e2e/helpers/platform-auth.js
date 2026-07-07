const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";

/** @param {import('@playwright/test').Page} page */
async function loginAsPlatformOps(page, {
  email = "ops@cureflow.in",
  password = "OpsAdmin123!",
} = {}) {
  await page.goto("/platform/login", { waitUntil: "domcontentloaded" });
  await page.getByTestId("platform-login-email").fill(email);
  await page.getByTestId("platform-login-password").fill(password);

  const loginResponse = page.waitForResponse(
    (r) => r.url().includes("/api/platform/auth/login") && r.request().method() === "POST",
    { timeout: 20_000 },
  );
  await page.getByTestId("platform-login-submit").click();
  const res = await loginResponse;
  if (!res.ok()) {
    throw new Error(`Platform login failed: ${res.status()} ${await res.text()}`);
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

const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";
const { apiLogin } = require("./api");

/** @param {import('@playwright/test').Page} page */
async function login(page, {
  email = "admin@cureflow.in",
  password = "admin123",
  useApi = true,
} = {}) {
  if (useApi) {
    const data = await apiLogin(email, password);
    const token = data.accessToken;
    const user = data.user;

    await page.goto("/login", { waitUntil: "domcontentloaded" });
    await page.evaluate(({ tokenValue, userValue }) => {
      localStorage.setItem("cureflow_token", tokenValue);
      localStorage.setItem("cureflow_user", JSON.stringify(userValue));
    }, { tokenValue: token, userValue: user });
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await page.waitForURL((url) => !url.pathname.includes("/login"), { timeout: 20_000 });
    return;
  }

  await page.goto("/login", { waitUntil: "domcontentloaded" });
  await page.getByTestId("login-email").fill(email);
  await page.getByTestId("login-password").fill(password);

  const loginResponse = page.waitForResponse(
    (r) => r.url().includes("/api/auth/login") && r.request().method() === "POST",
    { timeout: 20_000 },
  );
  await page.getByTestId("login-submit").click();
  const res = await loginResponse;
  if (!res.ok()) {
    throw new Error(`Login API failed: ${res.status()} ${await res.text()}`);
  }

  await page.waitForURL((url) => !url.pathname.includes("/login"), { timeout: 20_000 });
}

/** @param {import('@playwright/test').Page} page */
async function selectRadixOption(page, triggerTestId, optionText) {
  await page.getByTestId(triggerTestId).click();
  await page.getByRole("option", { name: optionText, exact: true }).click();
}

module.exports = { login, selectRadixOption };

const apiURL = process.env.PLAYWRIGHT_API_URL || "http://localhost:5180";
const { apiLogin } = require("./api");
const { HOSPITALS } = require("./multi-hospital");

/** Resolve hospital key, slug, or admin email to a seeded hospital fixture. */
function resolveHospital(slugOrEmail) {
  const needle = String(slugOrEmail).toLowerCase();
  return HOSPITALS.find(
    (h) =>
      h.key === needle ||
      h.slug === needle ||
      h.adminEmail.toLowerCase() === needle,
  );
}

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
    const tenant = data.tenant;

    await page.goto("/login", { waitUntil: "domcontentloaded" });
    await page.evaluate(({ tokenValue, userValue, tenantValue }) => {
      localStorage.setItem("cureflow_token", tokenValue);
      localStorage.setItem("cureflow_user", JSON.stringify(userValue));
      if (tenantValue) localStorage.setItem("cureflow_tenant", JSON.stringify(tenantValue));
    }, { tokenValue: token, userValue: user, tenantValue: tenant });
    // Root layout keeps AuthProvider mounted across client navigations; reload so
    // useState initializers pick up the seeded session (Next.js App Router).
    await page.reload({ waitUntil: "domcontentloaded" });
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
  const option = page.getByRole("option", { name: optionText, exact: true });
  await option.waitFor({ state: "visible", timeout: 10_000 });
  await option.click();
}

/**
 * Login as a seeded multi-hospital admin by key (althan/surat/pune), slug, or email.
 * @param {import('@playwright/test').Page} page
 * @param {string} slugOrEmail
 * @param {string} [password]
 */
async function loginAsHospital(page, slugOrEmail, password) {
  const hospital = resolveHospital(slugOrEmail);
  if (hospital) {
    return login(page, {
      email: hospital.adminEmail,
      password: password || hospital.password,
    });
  }
  if (!password) {
    throw new Error(`loginAsHospital: unknown hospital "${slugOrEmail}" and no password provided`);
  }
  return login(page, { email: slugOrEmail, password });
}

module.exports = { login, loginAsHospital, resolveHospital, selectRadixOption };

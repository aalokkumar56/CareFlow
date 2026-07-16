/**
 * Move the mouse visibly, then click via Playwright (handles Next.js client routing).
 * @param {import('@playwright/test').Page} page
 * @param {import('@playwright/test').Locator} locator
 */
async function humanClick(page, locator, { pauseMs = 150, moveSteps = 8, clickTimeout = 12_000, retries = 2 } = {}) {
  let lastErr;
  for (let attempt = 0; attempt < retries; attempt++) {
    try {
      await locator.scrollIntoViewIfNeeded({ timeout: 8_000 });
      await locator.waitFor({ state: "visible", timeout: 12_000 });
      const box = await locator.boundingBox();
      if (box) {
        const x = box.x + box.width / 2;
        const y = box.y + box.height / 2;
        await page.mouse.move(x, y, { steps: moveSteps });
        await page.waitForTimeout(pauseMs / 2);
      }
      await locator.click({ delay: 50, timeout: clickTimeout });
      await page.waitForTimeout(pauseMs);
      return;
    } catch (err) {
      lastErr = err;
      if (attempt < retries - 1) await page.waitForTimeout(300);
    }
  }
  throw lastErr;
}

/** Click a sidebar link and wait for client-side route change. */
async function humanNavClick(page, locator, expectedPath) {
  await locator.scrollIntoViewIfNeeded();
  await locator.waitFor({ state: "visible", timeout: 15_000 });
  const box = await locator.boundingBox();
  if (box) {
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 10 });
    await page.waitForTimeout(200);
  }
  await Promise.all([
    page.waitForURL(
      (url) => {
        if (expectedPath === "/") return url.pathname === "/";
        return url.pathname === expectedPath || url.pathname.startsWith(`${expectedPath}/`);
      },
      { timeout: 45_000, waitUntil: "domcontentloaded" },
    ),
    locator.click({ delay: 50 }),
  ]);
  await page.waitForTimeout(300);
}

/** Type like a user — one character at a time with small delays. */
async function humanType(locator, text, { delayMs = 40 } = {}) {
  await locator.click();
  await locator.fill("");
  await locator.pressSequentially(text, { delay: delayMs });
}

/** Login through the UI with mouse + keyboard (no API token injection). */
async function humanLogin(page, email, password) {
  await page.goto("/login", { waitUntil: "domcontentloaded" });
  await page.getByTestId("login-form").waitFor({ state: "visible", timeout: 60_000 });
  await humanType(page.getByTestId("login-email"), email);
  await humanType(page.getByTestId("login-password"), password);

  const loginResponse = page.waitForResponse(
    (r) => r.url().includes("/api/auth/login") && r.request().method() === "POST",
    { timeout: 60_000 },
  );
  await humanClick(page, page.getByTestId("login-submit"));
  const res = await loginResponse;
  if (!res.ok()) {
    throw new Error(`Login API failed: ${res.status()} ${await res.text()}`);
  }

  await page.waitForURL(
    (url) => !url.pathname.includes("/login"),
    { timeout: 60_000, waitUntil: "domcontentloaded" },
  );
  await page.getByTestId("app-sidebar").waitFor({ state: "visible", timeout: 30_000 });
}

/** Clear auth state and log in through the UI (reliable role switching in one browser). */
async function resetSessionAndLogin(page, email, password) {
  await page.context().clearCookies();
  await page.goto("/login", { waitUntil: "domcontentloaded" });
  await page.evaluate(() => {
    try {
      localStorage.clear();
      sessionStorage.clear();
    } catch {
      /* ignore */
    }
  });
  await humanLogin(page, email, password);
}

/** Logout via sidebar button — navigate home first so sidebar is stable. */
async function humanLogout(page) {
  await page.keyboard.press("Escape").catch(() => {});

  const sidebar = page.getByTestId("app-sidebar");
  if (await sidebar.isVisible().catch(() => false)) {
    const dashNav = sidebar.getByTestId("nav-dashboard");
    if (await dashNav.isVisible().catch(() => false)) {
      await humanNavClick(page, dashNav, "/").catch(() => {});
      await page.getByTestId("page-title").waitFor({ state: "visible", timeout: 15_000 }).catch(() => {});
    }

    const logoutBtn = page.getByTestId("logout-btn");
    if (await logoutBtn.isVisible().catch(() => false)) {
      await humanClick(page, logoutBtn);
      await page.waitForURL(/\/login/, { timeout: 30_000 });
      return;
    }
  }

  await page.context().clearCookies();
  await page.goto("/login", { waitUntil: "domcontentloaded" });
}

module.exports = { humanClick, humanNavClick, humanType, humanLogin, humanLogout, resetSessionAndLogin };

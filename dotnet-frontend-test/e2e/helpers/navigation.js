const { login } = require("./auth");

/** @param {import('@playwright/test').Page} page */
async function setViewport(page, viewport) {
  await page.setViewportSize({ width: viewport.width, height: viewport.height });
}

/**
 * Login as role and navigate; returns whether route is allowed for role.
 * @param {import('@playwright/test').Page} page
 */
async function gotoAsRole(page, { email, password, permissions }, path, { viewport, waitForApi } = {}) {
  if (viewport) await setViewport(page, viewport);
  await login(page, { email, password });

  let apiPromise;
  if (waitForApi) {
    apiPromise = page.waitForResponse(
      (r) => r.url().includes(waitForApi) && r.ok(),
      { timeout: 30_000 },
    ).catch(() => null);
  }

  await page.goto(path, { waitUntil: "domcontentloaded" });
  if (apiPromise) await apiPromise;
  await page.waitForTimeout(250);

  return { pathname: new URL(page.url()).pathname };
}

/** Wait for a list API response matching fragment. */
function waitForListApi(page, apiFragment) {
  return page.waitForResponse(
    (r) => r.url().includes(apiFragment) && r.request().method() === "GET" && r.ok(),
    { timeout: 30_000 },
  );
}

module.exports = { setViewport, gotoAsRole, waitForListApi };

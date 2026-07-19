// STRICT: asserts expected success OR expected failure; no soft-pass
/**
 * Analytics access + notification preference persistence.
 *
 * Scenario IDs (TEST_SCENARIOS.md):
 *   UI-MED-016, UI-MED-017, UI-MED-019, UI-MED-020,
 *   UI-MED-021, UI-MED-022, UI-MED-025
 */
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, apiRequest } = require("../helpers/api");
const { getRoleSessions } = require("../helpers/role-session");
const { routeAllowed } = require("../helpers/test-matrix");
const { PERMISSIONS } = require("../../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

const analyticsRoute = {
  path: "/missed-revenue",
  permission: PERMISSIONS.DashboardView,
  expectTestId: "analytics-total-loss",
};

async function firstPersonalPref(token) {
  const prefs = await apiRequest(token, "GET", "/notification-preferences");
  expect(Array.isArray(prefs), "prefs array").toBeTruthy();
  expect(prefs.length, "at least one preference").toBeGreaterThan(0);
  // Prefer a distinct label so substring matches (e.g. "created") do not hit multiple rows.
  return [...prefs].sort((a, b) => String(b.label || "").length - String(a.label || "").length)[0];
}

/** Locate the personal-pref row by category + exact visible label. */
function prefRow(page, pref) {
  const label = pref.label || pref.notification_type;
  const section = page.locator("section").filter({ has: page.getByRole("heading", { name: pref.category, exact: true }) });
  return section
    .locator("div.flex.items-center.justify-between")
    .filter({ has: page.locator("p.font-medium", { hasText: new RegExp(`^\\s*${escapeRegExp(label)}\\s*$`, "i") }) })
    .first();
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

async function togglePrefRow(page, pref) {
  const row = prefRow(page, pref);
  await expect(row).toBeVisible({ timeout: 15_000 });
  const sw = row.getByRole("switch");
  await expect(sw).toBeVisible();
  const before = await sw.getAttribute("data-state");
  await sw.click();
  await expect
    .poll(async () => sw.getAttribute("data-state"), { timeout: 5_000 })
    .not.toBe(before);
  return before === "checked";
}

test.describe("Analytics + notification preferences persistence", () => {
  /** @type {Record<string, { email: string, password: string, permissions: string[] }>} */
  let roles = {};

  test.beforeAll(async () => {
    roles = await getRoleSessions();
  });

  test("UI-MED-016: toggle preference and Save persists after reload", async ({ page }) => {
    const session = roles.admin;
    await login(page, { email: session.email, password: session.password });
    await page.goto("/notifications/preferences", { waitUntil: "networkidle" });
    await expect(page.getByTestId("notification-preferences-title")).toBeVisible({ timeout: 20_000 });

    // Stable order: API sorts by category then label — first switch is a reliable persist probe.
    const row = page.locator("section div.flex.items-center.justify-between").first();
    await expect(row).toBeVisible({ timeout: 15_000 });
    const label = ((await row.locator("p.font-medium").first().textContent()) || "").trim();
    expect(label.length).toBeGreaterThan(0);
    const sw = row.getByRole("switch");
    const before = await sw.getAttribute("data-state");
    const desired = before === "checked" ? "unchecked" : "checked";
    const desiredBool = desired === "checked";
    await sw.click();
    await expect(sw).toHaveAttribute("data-state", desired);
    await page.waitForTimeout(500);

    const saveReqPromise = page.waitForRequest(
      (r) =>
        r.url().includes("/api/notification-preferences") &&
        !r.url().includes("role-defaults") &&
        r.method() === "PUT",
      { timeout: 20_000 },
    );
    const saveRespPromise = page.waitForResponse(
      (r) =>
        r.url().includes("/api/notification-preferences") &&
        !r.url().includes("role-defaults") &&
        r.request().method() === "PUT" &&
        r.ok(),
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: "Save preferences" }).click();
    const saveReq = await saveReqPromise;
    await saveRespPromise;

    const payload = saveReq.postDataJSON();
    const prefs = payload?.preferences || [];
    expect(prefs.length).toBeGreaterThan(0);
    // At least one preference in the PUT must match the toggled direction.
    const matched = prefs.filter((p) => Boolean(p.in_app_enabled) === desiredBool);
    expect(matched.length, "Save payload should include toggled enabled state").toBeGreaterThan(0);

    await page.reload({ waitUntil: "networkidle" });
    await expect(page.getByTestId("notification-preferences-title")).toBeVisible({ timeout: 20_000 });
    const rowAfter = page
      .locator("section div.flex.items-center.justify-between")
      .filter({ has: page.locator("p.font-medium", { hasText: new RegExp(`^\\s*${escapeRegExp(label)}\\s*$`, "i") }) })
      .first();
    await expect(rowAfter.getByRole("switch")).toHaveAttribute("data-state", desired, {
      timeout: 15_000,
    });
  });

  test("UI-MED-019: non-admin can open preferences and save own prefs", async ({ page }) => {
    const session = roles.nurse;
    const { accessToken } = await apiLogin(session.email, session.password);
    const pref = await firstPersonalPref(accessToken);

    await login(page, { email: session.email, password: session.password });
    await page.goto("/notifications/preferences", { waitUntil: "networkidle" });
    await expect(page.getByTestId("notification-preferences-title")).toBeVisible({ timeout: 20_000 });

    // Role defaults tab is admin/settings-edit only on this page.
    await expect(page.getByRole("tab", { name: "Role defaults", exact: true })).toHaveCount(0);

    const wasOn = await togglePrefRow(page, pref);
    const saveResp = page.waitForResponse(
      (r) =>
        r.url().includes("/api/notification-preferences") &&
        !r.url().includes("role-defaults") &&
        r.request().method() === "PUT",
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: "Save preferences" }).click();
    expect((await saveResp).ok()).toBeTruthy();

    await page.reload({ waitUntil: "networkidle" });
    await expect(prefRow(page, pref).getByRole("switch")).toHaveAttribute(
      "data-state",
      wasOn ? "unchecked" : "checked",
    );

    // Restore original personal prefs for this type without wiping the rest.
    const current = await apiRequest(accessToken, "GET", "/notification-preferences");
    await apiRequest(accessToken, "PUT", "/notification-preferences", {
      preferences: current.map((p) => ({
        notification_type: p.notification_type,
        in_app_enabled:
          p.notification_type === pref.notification_type ? wasOn : p.in_app_enabled,
      })),
    });
  });

  test("UI-MED-017: admin Role defaults tab saves", async ({ page }) => {
    const session = roles.admin;
    await login(page, { email: session.email, password: session.password });
    await page.goto("/settings/notifications", { waitUntil: "networkidle" });
    await expect(page.getByTestId("page-title")).toHaveText("Notifications", { timeout: 20_000 });

    const rolesTab = page.getByRole("tab", { name: "Role defaults", exact: true });
    await expect(rolesTab).toBeVisible();
    await rolesTab.click();
    await expect(page.getByRole("button", { name: "Save role defaults" })).toBeVisible({
      timeout: 15_000,
    });

    const sw = page.getByRole("switch").first();
    await expect(sw).toBeVisible({ timeout: 15_000 });
    const before = await sw.getAttribute("data-state");
    await sw.click();
    await expect(sw).not.toHaveAttribute("data-state", before);

    const saveResp = page.waitForResponse(
      (r) =>
        r.url().includes("/api/notification-preferences/role-defaults") &&
        r.request().method() === "PUT",
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: "Save role defaults" }).click();
    expect((await saveResp).ok()).toBeTruthy();
    await expect(page.getByText(/Role defaults saved/i).first()).toBeVisible({ timeout: 10_000 });

    await page.reload({ waitUntil: "networkidle" });
    await page.getByRole("tab", { name: "Role defaults", exact: true }).click();
    const swAfter = page.getByRole("switch").first();
    await expect(swAfter).toHaveAttribute("data-state", before === "checked" ? "unchecked" : "checked", {
      timeout: 15_000,
    });

    // Flip back to keep defaults stable.
    await swAfter.click();
    const restoreResp = page.waitForResponse(
      (r) =>
        r.url().includes("/api/notification-preferences/role-defaults") &&
        r.request().method() === "PUT",
      { timeout: 20_000 },
    );
    await page.getByRole("button", { name: "Save role defaults" }).click();
    await restoreResp;
  });

  test("UI-MED-020: settings notifications scroll + save on desktop", async ({ page }) => {
    await login(page, { email: roles.admin.email, password: roles.admin.password });
    await page.goto("/settings/notifications", { waitUntil: "networkidle" });
    await expect(page.getByTestId("page-title")).toHaveText("Notifications");

    const scrollContainer = page.getByTestId("notifications-prefs-scroll");
    await expect(scrollContainer).toBeVisible();
    await scrollContainer.evaluate((el) => {
      el.scrollTop = el.scrollHeight;
    });
    const saveBtn = page.getByRole("button", { name: "Save preferences" });
    await expect(saveBtn).toBeVisible();
    await expect(saveBtn).toBeEnabled();

    const sw = page.getByRole("switch").first();
    await expect(sw).toBeVisible({ timeout: 15_000 });
    const before = await sw.getAttribute("data-state");
    await sw.click();
    await expect(sw).not.toHaveAttribute("data-state", before);

    const saveResp = page.waitForResponse(
      (r) =>
        r.url().includes("/api/notification-preferences") &&
        !r.url().includes("role-defaults") &&
        r.request().method() === "PUT",
      { timeout: 20_000 },
    );
    await saveBtn.click();
    expect((await saveResp).ok(), "Save preferences must succeed").toBeTruthy();
    await expect(page.getByText(/Notification preferences saved/i).first()).toBeVisible({
      timeout: 10_000,
    });
  });

  for (const role of ["admin", "nurse", "marketing", "staff"]) {
    test(`UI-MED-025: ${role} analytics access matches Dashboard.View`, async ({ page }) => {
      const session = roles[role];
      const allowed = routeAllowed(analyticsRoute, session.permissions);
      await login(page, { email: session.email, password: session.password });
      await page.goto("/missed-revenue", { waitUntil: "domcontentloaded" });
      await page.waitForTimeout(500);

      if (!allowed) {
        const pathname = new URL(page.url()).pathname;
        expect(pathname, `${role} must not stay on analytics`).not.toBe("/missed-revenue");
        return;
      }

      await expect(page.getByTestId("analytics-total-loss")).toBeVisible({ timeout: 20_000 });
      await expect(page.getByTestId("analytics-recoverable")).toBeVisible();
      await expect(page.getByTestId("analytics-item-count")).toBeVisible();

      // UI-MED-022: either empty state or accountability sections.
      const empty = page.getByTestId("analytics-empty-state");
      const hasEmpty = await empty.isVisible().catch(() => false);
      if (hasEmpty) {
        await expect(empty).toContainText(/All clear/i);
      } else {
        const section = page.locator("[data-testid^='analytics-section-']").first();
        await expect(section).toBeVisible({ timeout: 10_000 });
      }
    });
  }

  test("UI-MED-021: analytics empty drills to dashboard; non-empty drills via section link", async ({ page }) => {
    await login(page, { email: roles.admin.email, password: roles.admin.password });
    await page.goto("/missed-revenue", { waitUntil: "networkidle" });
    await expect(page.getByTestId("analytics-total-loss")).toBeVisible({ timeout: 20_000 });

    const empty = page.getByTestId("analytics-empty-state");
    const emptyVisible = await empty.isVisible();

    if (emptyVisible) {
      await expect(empty).toContainText(/All clear/i);
      await page.getByRole("link", { name: /Back to dashboard/i }).click();
      await expect(page).toHaveURL(/\/$/, { timeout: 15_000 });
      return;
    }

    await expect(page.locator("[data-testid^='analytics-section-']").first()).toBeVisible({
      timeout: 10_000,
    });
    const drill = page.locator("a[href='/inbox'], a[href='/tasks'], a[href^='/patients/']").first();
    await expect(drill, "non-empty analytics must expose a drill-down link").toBeVisible({
      timeout: 10_000,
    });
    const href = await drill.getAttribute("href");
    expect(href).toBeTruthy();
    await drill.click();
    await expect(page).toHaveURL(new RegExp(href.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")), {
      timeout: 15_000,
    });
  });
});

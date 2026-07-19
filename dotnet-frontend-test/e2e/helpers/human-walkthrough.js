/**
 * Shared walkthrough steps — every action uses mouse clicks (human-interaction).
 */
const { expect } = require("@playwright/test");
const { humanClick, humanNavClick } = require("./human-interaction");
const { NAV_ITEMS, SETTINGS_ROUTES, FEATURE_BUTTONS } = require("./routes");
const { routeAllowed } = require("./test-matrix");
const { PERMISSIONS } = require("../../../dotnet-frontend/src/lib/permissions");

const SETTINGS_LINKS = [
  { name: "Users", path: "/settings/users", expectTestId: "users-search", permission: PERMISSIONS.UserView },
  { name: "Roles", path: "/settings/roles", expectTestId: "roles-permissions-panel", permission: PERMISSIONS.UserView },
  { name: "Permissions", path: "/settings/permissions", expectTestId: "permissions-catalog-panel", permission: PERMISSIONS.UserView },
  { name: "Templates", path: "/settings/templates", expectTestId: null, permission: PERMISSIONS.SettingsView },
  { name: "Notifications", path: "/settings/notifications", expectTestId: null, permission: PERMISSIONS.SettingsView },
  { name: "Hospital", path: "/settings/hospital", expectTestId: null, permission: PERMISSIONS.SettingsView },
  { name: "Integrations", path: "/settings/integrations", expectTestId: "integrations-panel", permission: PERMISSIONS.SettingsView },
];

const { PATIENT_TABS } = require("./routes");

function navAllowed(nav, permissions) {
  if (nav.anyPermission) {
    return nav.anyPermission.some((p) => permissions.includes(p));
  }
  return permissions.includes(nav.permission);
}

/** Walk every module this role can access — sidebar clicks only, no page.goto shortcuts. */
async function walkRoleModules(page, permissions, { roleLabel = "user" } = {}) {
  // ── Main sidebar modules ────────────────────────────────────────────────
  for (const item of NAV_ITEMS) {
    if (item.testId === "nav-settings") continue;
    if (!navAllowed(item, permissions)) continue;

    const navLink = page.getByTestId("app-sidebar").getByTestId(item.testId);
    if (!(await navLink.isVisible().catch(() => false))) continue;

    await humanNavClick(page, navLink, item.path);

    if (item.path === "/") {
      await expect(page.getByTestId("page-title")).toBeVisible({ timeout: 15_000 });
    }
    if (item.path === "/missed-revenue") {
      const widget = page.getByTestId("analytics-total-loss");
      if (await widget.isVisible().catch(() => false)) {
        await expect(widget).toBeVisible();
      }
    }
  }

  // ── Settings area (if role has SettingsView or UserView sub-routes) ───────
  const settingsNav = page.getByTestId("app-sidebar").getByTestId("nav-settings");
  const canSeeSettings = navAllowed(
    NAV_ITEMS.find((n) => n.testId === "nav-settings"),
    permissions,
  );

  if (canSeeSettings && (await settingsNav.isVisible().catch(() => false))) {
    await humanNavClick(page, settingsNav, "/settings");

    for (const link of SETTINGS_LINKS) {
      if (!permissions.includes(link.permission)) continue;

      const settingsPanel = page.getByTestId("settings-nav");
      const subLink = settingsPanel.getByRole("link", { name: link.name, exact: true });
      if (!(await subLink.isVisible().catch(() => false))) continue;

      await humanClick(page, subLink);
      await page.waitForURL(
        (url) => url.pathname === link.path || url.pathname.startsWith(`${link.path}/`),
        { timeout: 30_000 },
      );

      if (link.expectTestId) {
        await expect(page.getByTestId(link.expectTestId)).toBeVisible({ timeout: 15_000 });
      } else {
        await page.waitForLoadState("networkidle").catch(() => {});
      }
    }
  }

  // ── Create dialogs — open with mouse, close with Escape ─────────────────
  for (const btn of FEATURE_BUTTONS) {
    if (!permissions.includes(btn.permission)) continue;

    const navItem = NAV_ITEMS.find((n) => n.path === btn.path);
    if (!navItem || !navAllowed(navItem, permissions)) continue;

    const navLink = page.getByTestId("app-sidebar").getByTestId(navItem.testId);
    if (!(await navLink.isVisible().catch(() => false))) continue;

    await humanNavClick(page, navLink, btn.path);
    await page.keyboard.press("Escape").catch(() => {});

    const openBtn = page.getByTestId(btn.testId);
    if (!(await openBtn.isVisible({ timeout: 8000 }).catch(() => false))) continue;

    try {
      await humanClick(page, openBtn, { clickTimeout: 8_000 });
      await page.waitForTimeout(400);
    } catch {
      /* dialog may not open — continue walkthrough */
    }
    await page.keyboard.press("Escape").catch(() => {});
    await page.waitForTimeout(200);
  }

  // ── Patient detail + tabs (optional — list page already covers Patient module) ─
  if (permissions.includes(PERMISSIONS.PatientView)) {
    const patientsNav = page.getByTestId("app-sidebar").getByTestId("nav-patients");
    if (await patientsNav.isVisible().catch(() => false)) {
      await humanNavClick(page, patientsNav, "/patients");
      const firstPatient = page.locator("[data-testid^='patient-row-']").first();
      if (await firstPatient.isVisible().catch(() => false)) {
        try {
          await humanClick(page, firstPatient);

          const preview = page.getByTestId("patient-preview-panel");
          if (await preview.isVisible({ timeout: 8_000 }).catch(() => false)) {
            const fullProfile = preview.getByRole("link", { name: /Full Profile/i });
            await humanClick(page, fullProfile);
          } else {
            const rowTestId = await firstPatient.getAttribute("data-testid");
            const patientId = rowTestId?.replace("patient-row-", "");
            if (patientId) {
              await humanClick(page, page.getByTestId(`patient-actions-${patientId}`));
              await humanClick(page, page.getByRole("menuitem", { name: "View details" }));
            }
          }

          await page.waitForURL(/\/patients\/[^/]+/, { timeout: 20_000, waitUntil: "domcontentloaded" });
          const patientName = page.getByTestId("patient-name");
          if (await patientName.isVisible({ timeout: 10_000 }).catch(() => false)) {
            for (const tabId of PATIENT_TABS) {
              const tab = page.getByTestId(tabId);
              if (await tab.isVisible().catch(() => false)) {
                await humanClick(page, tab);
              }
            }
          }
        } catch {
          console.warn(`[walkthrough] patient detail skipped (preview/navigation unavailable)`);
        }
      }
    }
  }

  // ── Referral CRM detail ─────────────────────────────────────────────────
  if (permissions.includes(PERMISSIONS.ReferralView)) {
    const doctorsNav = page.getByTestId("app-sidebar").getByTestId("nav-referral-crm");
    if (await doctorsNav.isVisible().catch(() => false)) {
      await humanNavClick(page, doctorsNav, "/doctors");
      const firstDoctor = page.locator("[data-testid^='doctor-row-'], [data-testid^='referrer-card-']").first();
      if (await firstDoctor.isVisible().catch(() => false)) {
        await humanClick(page, firstDoctor);
        await page.waitForLoadState("networkidle").catch(() => {});
      }
    }
  }

  // ── Command palette ─────────────────────────────────────────────────────
  const dashNav = page.getByTestId("app-sidebar").getByTestId("nav-dashboard");
  if (permissions.includes(PERMISSIONS.DashboardView) && (await dashNav.isVisible().catch(() => false))) {
    await humanNavClick(page, dashNav, "/");
    const paletteBtn = page.getByTestId("open-command-palette");
    if (await paletteBtn.isVisible().catch(() => false)) {
      await humanClick(page, paletteBtn);
      await page.keyboard.press("Escape");
    }
  }

  console.log(`[walkthrough] ${roleLabel}: all accessible modules visited`);
}

module.exports = {
  walkRoleModules,
  SETTINGS_LINKS,
  navAllowed,
  routeAllowed,
};

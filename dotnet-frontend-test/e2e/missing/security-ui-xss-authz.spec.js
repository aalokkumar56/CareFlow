/**
 * UI security — XSS rendering, role authz, deep-link escalation, logout token wipe,
 * and command-palette navigation into forbidden routes.
 *
 * Scenario IDs (see TEST_SCENARIOS.md when present):
 *   UI-SEC-XSS-001, UI-SEC-AUTHZ-001/002, UI-SEC-ESCALATE-001,
 *   UI-SEC-LOGOUT-001, UI-SEC-PALETTE-001
 */
const { test, expect } = require("@playwright/test");
const { login } = require("../helpers/auth");
const { apiLogin, createPatient } = require("../helpers/api");
const { ensureTestUsers } = require("../helpers/test-users");
const { routeAllowed } = require("../helpers/test-matrix");
const { getPermissionsForUser, PERMISSIONS } = require("../../../dotnet-frontend/src/lib/permissions");

test.describe.configure({ mode: "serial" });

const XSS_MARKER = "__cf_xss_probe__";
const XSS_PAYLOADS = [
  `<img src=x onerror="window.${XSS_MARKER}=1">`,
  `<script>window.${XSS_MARKER}=1</script>`,
  `"><svg/onload="window.${XSS_MARKER}=1">`,
];

/** High-value routes nurse/marketing must not keep open. */
const FORBIDDEN_BY_ROLE = {
  nurse: [
    { path: "/inbox", label: "WhatsApp Inbox", permission: PERMISSIONS.ConversationView },
    { path: "/campaigns", label: "Campaigns", permission: PERMISSIONS.CampaignView },
    { path: "/settings/users", label: "Settings Users", permission: PERMISSIONS.UserView },
    { path: "/doctors", label: "Referral CRM", permission: PERMISSIONS.ReferralView },
  ],
  marketing: [
    { path: "/appointments", label: "Appointments", permission: PERMISSIONS.AppointmentView },
    { path: "/inbox", label: "WhatsApp Inbox", permission: PERMISSIONS.ConversationView },
    { path: "/settings/users", label: "Settings Users", permission: PERMISSIONS.UserView },
    { path: "/staff", label: "Hospital Staff", anyPermission: [PERMISSIONS.StaffView, PERMISSIONS.ClinicalView] },
  ],
};

const DEEP_LINK_ESCALATIONS = [
  { path: "/settings/users?role=admin", base: "/settings/users" },
  { path: "/settings/roles", base: "/settings/roles" },
  { path: "/settings/integrations", base: "/settings/integrations" },
  { path: "/campaigns?new=1", base: "/campaigns", roles: ["nurse"] },
  { path: "/doctors", base: "/doctors" },
];

const PALETTE_FORBIDDEN_BY_ROLE = {
  nurse: [{ label: "WhatsApp Inbox", path: "/inbox" }],
  marketing: [{ label: "Appointments", path: "/appointments" }],
};

function patientIdFrom(created) {
  return created?.id || created?.patient?.id;
}

async function storageKeys(page) {
  return page.evaluate(() => ({
    token: localStorage.getItem("cureflow_token"),
    user: localStorage.getItem("cureflow_user"),
    tenant: localStorage.getItem("cureflow_tenant"),
  }));
}

/** Navigate with a short retry — sibling agents sometimes bounce the Next.js dev server. */
async function gotoResilient(page, path, attempts = 3) {
  let lastErr;
  for (let i = 0; i < attempts; i += 1) {
    try {
      await page.goto(path, { waitUntil: "domcontentloaded", timeout: 30_000 });
      return;
    } catch (err) {
      lastErr = err;
      const msg = String(err?.message || err);
      if (!/CONNECTION_REFUSED|CONNECTION_RESET|ERR_EMPTY_RESPONSE/i.test(msg) || i === attempts - 1) {
        throw err;
      }
      await page.waitForTimeout(1500 * (i + 1));
    }
  }
  throw lastErr;
}

async function openCommandPalette(page) {
  await gotoResilient(page, "/");
  const btn = page
    .getByTestId("open-command-palette")
    .or(page.getByTestId("open-command-palette-mobile"))
    .first();
  await expect(btn).toBeVisible({ timeout: 20_000 });
  await btn.click();
  await expect(page.getByTestId("cmd-palette-input")).toBeVisible({ timeout: 10_000 });
}

async function expectRedirectedAway(page, forbiddenPath) {
  const base = forbiddenPath.split("?")[0];
  await expect
    .poll(() => new URL(page.url()).pathname, { timeout: 15_000 })
    .not.toBe(base);
  const pathname = new URL(page.url()).pathname;
  expect(pathname.startsWith(`${base}/`)).toBeFalsy();
}

test.describe("Security UI — XSS, authz, logout, command palette", () => {
  /** @type {Record<string, { email: string, password: string, permissions: string[], token?: string }>} */
  let roles = {};

  test.beforeAll(async () => {
    const users = await ensureTestUsers(Date.now());
    for (const role of ["admin", "nurse", "marketing"]) {
      const creds = users[role];
      const session = await apiLogin(creds.email, creds.password);
      roles[role] = {
        email: creds.email,
        password: creds.password,
        permissions: getPermissionsForUser(session.user),
        token: session.accessToken,
      };
    }
  });

  test("XSS payloads in patient name do not execute", async ({ page }) => {
    const dialogs = [];
    page.on("dialog", async (d) => {
      dialogs.push(d.message());
      await d.dismiss().catch(() => {});
    });

    const admin = roles.admin;
    await login(page, { email: admin.email, password: admin.password });

    for (let i = 0; i < XSS_PAYLOADS.length; i += 1) {
      const payload = XSS_PAYLOADS[i];
      const phone = `917${String(Date.now() + i).slice(-9)}`;
      const created = await createPatient(admin.token, payload, phone);
      const id = patientIdFrom(created);
      expect(id, "patient id from create").toBeTruthy();

      await gotoResilient(page, `/patients/${id}`);
      const nameEl = page.getByTestId("patient-name");
      await expect(nameEl).toBeVisible({ timeout: 20_000 });
      await expect(nameEl).toHaveText(payload);

      const nameHtml = await nameEl.innerHTML();
      expect(nameHtml, "name rendered via raw HTML tags").not.toMatch(/<(script|img|svg)\b/i);

      const markerSet = await page.evaluate((marker) => Boolean(window[marker]), XSS_MARKER);
      expect(markerSet, `XSS marker set for payload: ${payload}`).toBeFalsy();
    }

    await gotoResilient(page, "/patients");
    await expect(page.getByTestId("patients-search")).toBeVisible({ timeout: 20_000 });
    const listMarker = await page.evaluate((marker) => Boolean(window[marker]), XSS_MARKER);
    expect(listMarker).toBeFalsy();
    expect(await page.locator("img[onerror], svg[onload]").count()).toBe(0);
    expect(dialogs, "browser alert/confirm from XSS").toEqual([]);
  });

  for (const role of ["nurse", "marketing"]) {
    test(`${role}: forbidden routes redirect away`, async ({ page }) => {
      const session = roles[role];
      await login(page, { email: session.email, password: session.password });

      const forbidden = FORBIDDEN_BY_ROLE[role].filter((r) => !routeAllowed(r, session.permissions));
      expect(forbidden.length, `${role} should have forbidden routes`).toBeGreaterThan(0);

      for (const route of forbidden) {
        await gotoResilient(page, route.path);
        await expectRedirectedAway(page, route.path);
      }
    });
  }

  test("privilege escalation via deep links is blocked", async ({ page }) => {
    for (const role of ["nurse", "marketing"]) {
      const session = roles[role];
      await login(page, { email: session.email, password: session.password });

      for (const link of DEEP_LINK_ESCALATIONS) {
        if (link.roles && !link.roles.includes(role)) continue;
        await gotoResilient(page, link.path);
        await expectRedirectedAway(page, link.base);
      }
    }
  });

  test("logout clears sensitive tokens from storage", async ({ page }) => {
    await login(page, { email: roles.admin.email, password: roles.admin.password });
    await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 20_000 });

    const before = await storageKeys(page);
    expect(before.token, "token present after login").toBeTruthy();
    expect(before.user, "user present after login").toBeTruthy();

    await page.getByTestId("logout-btn").click();
    await expect(page).toHaveURL(/\/login/, { timeout: 20_000 });

    const after = await storageKeys(page);
    expect(after.token, "cureflow_token cleared").toBeNull();
    expect(after.user, "cureflow_user cleared").toBeNull();
    expect(after.tenant, "cureflow_tenant cleared").toBeNull();

    await gotoResilient(page, "/patients");
    await expect(page).toHaveURL(/\/login/, { timeout: 15_000 });
  });

  for (const role of ["nurse", "marketing"]) {
    test(`${role}: command palette cannot open forbidden routes`, async ({ page }) => {
      const session = roles[role];
      await login(page, { email: session.email, password: session.password });

      for (const item of PALETTE_FORBIDDEN_BY_ROLE[role]) {
        await openCommandPalette(page);
        const dialog = page.getByRole("dialog");
        const cmdItem = dialog.locator("[cmdk-item]").filter({ hasText: item.label }).first();
        await expect(cmdItem).toBeVisible({ timeout: 10_000 });
        await cmdItem.click();
        await expectRedirectedAway(page, item.path);
      }

      if (role === "marketing") {
        await openCommandPalette(page);
        const dialog = page.getByRole("dialog");
        const newAppt = dialog.locator("[cmdk-item]").filter({ hasText: "New Appointment" }).first();
        if (await newAppt.isVisible().catch(() => false)) {
          await newAppt.click();
          await expectRedirectedAway(page, "/appointments");
        }
      }
    });
  }
});

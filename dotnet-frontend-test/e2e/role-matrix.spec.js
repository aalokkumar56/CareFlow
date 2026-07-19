const { test } = require("@playwright/test");
const { login } = require("./helpers/auth");
const { apiLogin } = require("./helpers/api");
const { ensureTestUsers, ROLES } = require("./helpers/test-users");
const {
  MAIN_ROUTES,
  SETTINGS_ROUTES,
  NAV_ITEMS,
  FEATURE_BUTTONS,
  ALL_ROLES_MATRIX,
} = require("./helpers/routes");
const { getPermissionsForUser, PERMISSIONS } = require("../../dotnet-frontend/src/lib/permissions");
const { reportBug } = require("./helpers/bug-log");

test.describe.configure({ mode: "serial" });

function hasPerm(perms, permission) {
  return perms.includes(permission);
}

function routeAllowed(route, perms) {
  if (route.anyPermission) {
    return route.anyPermission.some((p) => perms.includes(p));
  }
  if (route.permission) return perms.includes(route.permission);
  return true;
}

test.describe("Role matrix — permissions from JWT", () => {
  /** @type {Record<string, { email: string, password: string, permissions: string[] }>} */
  let roleData = {};

  test.beforeAll(async () => {
    const stamp = Date.now();
    const users = await ensureTestUsers(stamp);
    roleData.admin = {
      email: users.admin.email,
      password: users.admin.password,
      permissions: getPermissionsForUser((await apiLogin(users.admin.email, users.admin.password)).user),
    };

    for (const role of ROLES) {
      const creds = users[role];
      const { user } = await apiLogin(creds.email, creds.password);
      roleData[role] = {
        email: creds.email,
        password: creds.password,
        permissions: getPermissionsForUser(user),
      };
    }
  });

  for (const role of ALL_ROLES_MATRIX) {
    test(`${role}: nav visibility matches JWT permissions`, async ({ page }) => {
      const { email, password, permissions } = roleData[role];
      await login(page, { email, password });

      for (const nav of NAV_ITEMS) {
        const shouldSee = nav.anyPermission
          ? nav.anyPermission.some((p) => permissions.includes(p))
          : permissions.includes(nav.permission);

        const navEl = page.getByTestId("app-sidebar").getByTestId(nav.testId);
        const visible = await navEl.isVisible().catch(() => false);

        if (shouldSee && !visible) {
          reportBug({
            severity: "High",
            page: "sidebar",
            title: `${role}: missing nav ${nav.testId}`,
            expected: "Visible per JWT permissions",
            actual: "Hidden",
            steps: `Login as ${role}, check ${nav.testId}`,
          });
        }
        if (!shouldSee && visible) {
          reportBug({
            severity: "High",
            page: "sidebar",
            title: `${role}: unauthorized nav ${nav.testId} visible`,
            expected: "Hidden",
            actual: "Visible",
          });
        }
      }
    });

    test(`${role}: forbidden routes redirect`, async ({ page }) => {
      const { email, password, permissions } = roleData[role];
      await login(page, { email, password });

      const allRoutes = [
        ...MAIN_ROUTES,
        ...SETTINGS_ROUTES.filter((r) => r.path !== "/notifications/preferences"),
      ];

      for (const route of allRoutes) {
        const allowed = routeAllowed(route, permissions);
        await page.goto(route.path, { waitUntil: "domcontentloaded" });
        await page.waitForTimeout(350);

        const pathname = new URL(page.url()).pathname;
        if (!allowed && pathname === route.path) {
          reportBug({
            severity: "Critical",
            page: route.path,
            title: `${role} accessed forbidden route ${route.label}`,
            steps: `Login as ${role}, navigate to ${route.path}`,
            expected: "Redirect away from page",
            actual: `Stayed on ${pathname}`,
          });
        }
      }
    });

    test(`${role}: feature buttons respect permissions`, async ({ page }) => {
      const { email, password, permissions } = roleData[role];
      await login(page, { email, password });

      for (const btn of FEATURE_BUTTONS) {
        const routeAllowedForPage = MAIN_ROUTES.find((r) => r.path === btn.path)
          || SETTINGS_ROUTES.find((r) => r.path === btn.path);
        if (routeAllowedForPage && !routeAllowed(routeAllowedForPage, permissions)) {
          continue;
        }

        await page.goto(btn.path, { waitUntil: "networkidle" });
        await page.waitForTimeout(300);

        const el = page.getByTestId(btn.testId);
        const visible = await el.isVisible().catch(() => false);
        const shouldSee = hasPerm(permissions, btn.permission);

        if (shouldSee && !visible) {
          reportBug({
            severity: "High",
            page: btn.path,
            title: `${role}: missing ${btn.label} button`,
            expected: `${btn.testId} visible (has ${btn.permission})`,
            actual: "Hidden",
          });
        }
        if (!shouldSee && visible) {
          reportBug({
            severity: "High",
            page: btn.path,
            title: `${role}: unauthorized ${btn.label} button visible`,
            expected: "Hidden",
            actual: `${btn.testId} visible without ${btn.permission}`,
          });
        }
      }
    });
  }

  test("marketing: cannot access settings users", async ({ page }) => {
    const { email, password } = roleData.marketing;
    await login(page, { email, password });
    await page.goto("/settings/users");
    await page.waitForTimeout(500);
    if (new URL(page.url()).pathname === "/settings/users") {
      reportBug({
        severity: "Critical",
        page: "/settings/users",
        title: "Marketing accessed user management",
        expected: "Redirect",
        actual: "Page accessible",
      });
    }
  });

  test("nurse: no WhatsApp icon on patient detail", async ({ page }) => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const { createPatient } = require("./helpers/api");
    const patient = await createPatient(admin.accessToken, `Role Matrix ${Date.now()}`, `91${String(Date.now()).slice(-8)}`);
    const patientId = patient.id || patient.patient?.id;
    if (!patientId) return;

    const { email, password, permissions } = roleData.nurse;
    const canWhatsApp = permissions.includes(PERMISSIONS.WhatsAppView) || permissions.includes(PERMISSIONS.WhatsAppSend);

    await login(page, { email, password });
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });
    const waVisible = await page.getByTestId("patient-whatsapp-icon").isVisible().catch(() => false);

    if (!canWhatsApp && waVisible) {
      reportBug({
        severity: "Medium",
        page: `/patients/${patientId}`,
        title: "Nurse sees WhatsApp icon without permission",
        expected: "Hidden",
        actual: "Visible",
      });
    }
    if (canWhatsApp && !waVisible) {
      reportBug({
        severity: "Medium",
        page: `/patients/${patientId}`,
        title: "Nurse should see WhatsApp icon per JWT",
        expected: "Visible",
        actual: "Hidden",
      });
    }
  });

  test("reception: WhatsApp icon visible on patient detail", async ({ page }) => {
    const admin = await apiLogin("admin@cureflow.in", "admin123");
    const { createPatient } = require("./helpers/api");
    const patient = await createPatient(admin.accessToken, `Reception WA ${Date.now()}`, `91${String(Date.now()).slice(-8)}`);
    const patientId = patient.id || patient.patient?.id;
    if (!patientId) return;

    const { email, password, permissions } = roleData.reception;
    const canWhatsApp = permissions.includes(PERMISSIONS.WhatsAppView) || permissions.includes(PERMISSIONS.WhatsAppSend);

    await login(page, { email, password });
    await page.goto(`/patients/${patientId}`, { waitUntil: "networkidle" });

    if (canWhatsApp) {
      const waVisible = await page.getByTestId("patient-whatsapp-icon").isVisible().catch(() => false);
      if (!waVisible) {
        reportBug({
          severity: "High",
          page: `/patients/${patientId}`,
          title: "Reception missing WhatsApp icon",
          expected: "patient-whatsapp-icon visible",
          actual: "Not visible",
        });
      }
    }
  });
});

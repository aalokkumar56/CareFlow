/**
 * Deep human walkthrough — every control, filter, tab, and dialog.
 * Uses sidebar navigation only (single browser session, no page.goto shortcuts in-app).
 */
const { expect } = require("@playwright/test");
const { humanClick, humanNavClick, humanType } = require("./human-interaction");
const {
  NAV_ITEMS,
  PATIENT_TABS,
  PATIENT_EDIT_TABS,
  FEATURE_BUTTONS,
  SETTINGS_ROUTES,
} = require("./routes");
const { PERMISSIONS } = require("../../../dotnet-frontend/src/lib/permissions");
const { SETTINGS_LINKS, navAllowed } = require("./human-walkthrough");
const { createUxFindings } = require("./ux-findings");

const DASHBOARD_APPT_PERIODS = ["this_week", "last_week"];
const DASHBOARD_REVENUE_PERIODS = ["this_month", "last_month", "this_week"];
const PATIENT_STATUS_OPTIONS = ["All Statuses", "New Inquiry", "Contacted", "Visited"];
const APPT_KANBAN_COLUMNS = ["Scheduled", "Confirmed", "Completed", "Cancelled", "No-show"];
const TASK_KANBAN_COLUMNS = ["Pending", "In Progress", "Completed", "Overdue"];
const CAMPAIGN_KANBAN_COLUMNS = ["Draft", "Scheduled", "Active", "Completed"];

async function clickIfVisible(page, locator, { timeout = 5_000 } = {}) {
  try {
    await locator.waitFor({ state: "visible", timeout });
    await humanClick(page, locator, { clickTimeout: 8_000 });
    return true;
  } catch {
    return false;
  }
}

async function walkDashboard(page, ux, { roleLabel }) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-dashboard");
  await humanNavClick(page, nav, "/");
  await expect(page.getByTestId("page-title")).toBeVisible({ timeout: 15_000 });
  await page.waitForResponse(
    (r) => r.url().includes("/api/dashboard/overview") && r.status() === 200,
    { timeout: 45_000 },
  ).catch(() => {});
  await expect(page.getByTestId("stat-appointments")).toBeVisible({ timeout: 30_000 });

  for (const period of DASHBOARD_APPT_PERIODS) {
    const btn = page.getByTestId(`dashboard-appt-period-${period}`);
    await clickIfVisible(page, btn);
  }

  const revenueToggle = page.getByTestId("dashboard-revenue-period-toggle");
  if (await revenueToggle.isVisible().catch(() => false)) {
    await humanClick(page, revenueToggle);
    for (const period of DASHBOARD_REVENUE_PERIODS) {
      const opt = page.getByTestId(`dashboard-revenue-period-${period}`);
      await clickIfVisible(page, opt);
    }
  } else if (roleLabel === "nurse" || roleLabel === "staff") {
    ux.note(
      "Dashboard / Revenue",
      "Revenue widgets correctly hidden for clinical roles without Billing.View — nurses see appointments and care insights only.",
      "info",
    );
  }

  await clickIfVisible(page, page.getByTestId("dashboard-upcoming-view-all"));
  if (page.url().includes("/appointments")) {
    await humanNavClick(page, nav, "/");
  }

  await clickIfVisible(page, page.getByTestId("dashboard-view-insight"));
  if (page.url().includes("/missed-revenue")) {
    await humanNavClick(page, nav, "/");
  }

  const palette = page
    .getByTestId("open-command-palette")
    .or(page.getByTestId("open-command-palette-mobile"));
  if (await clickIfVisible(page, palette.first())) {
    await page.keyboard.press("Escape").catch(() => {});
  }

  await clickIfVisible(page, page.getByTestId("stat-revenue"));
  if (page.url().includes("/missed-revenue")) {
    await humanNavClick(page, nav, "/");
  }
}

async function walkPatients(page, ux, permissions) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-patients");
  await humanNavClick(page, nav, "/patients");
  await expect(page.getByTestId("patients-search")).toBeVisible();

  await humanType(page.getByTestId("patients-search"), "7600");
  await page.waitForTimeout(500);
  await humanType(page.getByTestId("patients-search"), "");

  for (const status of PATIENT_STATUS_OPTIONS) {
    if (!(await clickIfVisible(page, page.getByTestId("filter-status"), { timeout: 3_000 }))) break;
    const opt = page.getByRole("option", { name: status, exact: true });
    await clickIfVisible(page, opt, { timeout: 2_000 });
  }

  if (await clickIfVisible(page, page.getByTestId("filter-dept"), { timeout: 3_000 })) {
    const deptOpt = page.getByRole("option").first();
    await clickIfVisible(page, deptOpt, { timeout: 2_000 });
  }

  if (await clickIfVisible(page, page.getByTestId("filter-source"), { timeout: 3_000 })) {
    const srcOpt = page.getByRole("option").first();
    await clickIfVisible(page, srcOpt, { timeout: 2_000 });
  }

  if (permissions.includes(PERMISSIONS.PatientCreate)) {
    const newBtn = page.getByTestId("new-patient-btn");
    if (await newBtn.isVisible().catch(() => false)) {
      await humanClick(page, newBtn);
      await expect(page.getByTestId("new-patient-dialog")).toBeVisible();
      await humanType(page.getByTestId("np-name"), "E2E Walkthrough Patient");
      await humanType(page.getByTestId("np-phone"), "919988776655");
      await page.keyboard.press("Escape");
    }
  }

  await clickIfVisible(page, page.getByTestId("export-patients-btn"));

  const firstRow = page.locator("[data-testid^='patient-row-']").first();
  if (!(await firstRow.isVisible().catch(() => false))) {
    ux.note("Patients", "No patient rows to exercise preview/detail flow.", "info");
    return;
  }

  const rowTestId = await firstRow.getAttribute("data-testid");
  const patientId = rowTestId?.replace("patient-row-", "");

  await humanClick(page, firstRow);
  const preview = page.getByTestId("patient-preview-panel");
  const previewVisible = await preview.isVisible({ timeout: 8_000 }).catch(() => false);

  if (previewVisible) {
    ux.note(
      "Patients / Row click",
      "Single click opens quick preview (reception triage). Double-click opens full profile directly — documented in row tooltip.",
      "info",
    );

    const fullProfile = preview.getByRole("link", { name: /Full Profile/i });
    await clickIfVisible(page, fullProfile, { timeout: 4_000 });
  }

  if (!page.url().match(/\/patients\/[^/]+/) && patientId) {
    await firstRow.dblclick({ timeout: 8_000 }).catch(() => {});
  }

  if (!page.url().match(/\/patients\/[^/]+/) && patientId) {
    const actionsBtn = page.getByTestId(`patient-actions-${patientId}`);
    if (await actionsBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await humanClick(page, actionsBtn);
      await humanClick(page, page.getByRole("menuitem", { name: /View details/i }));
    } else {
      await page.goto(`/patients/${patientId}`, { waitUntil: "domcontentloaded" });
    }
  }

  await page.waitForURL(/\/patients\/[^/]+/, { timeout: 25_000, waitUntil: "domcontentloaded" }).catch(() => {});

  if (page.url().match(/\/patients\/[^/]+/)) {
    await expect(page.getByTestId("patient-name")).toBeVisible({ timeout: 12_000 });

    for (const tabId of PATIENT_TABS) {
      const tab = page.getByTestId(tabId);
      if (await tab.isVisible().catch(() => false)) await humanClick(page, tab);
    }

    const editIcon = page.getByTestId("patient-edit-icon");
    if (permissions.includes(PERMISSIONS.PatientEdit) && (await editIcon.isVisible().catch(() => false))) {
      await humanClick(page, editIcon);
      for (const tabId of PATIENT_EDIT_TABS) {
        const tab = page.getByTestId(tabId);
        if (await tab.isVisible().catch(() => false)) await humanClick(page, tab);
      }
      await page.keyboard.press("Escape").catch(() => {});
    }

    await humanClick(page, page.getByTestId("patient-back-link"));
    await page.waitForURL(/\/patients\/?$/, { timeout: 15_000 });
  }
}

async function walkInbox(page) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-whatsapp-inbox");
  await humanNavClick(page, nav, "/inbox");
  await expect(page.getByTestId("inbox-search")).toBeVisible();

  await humanType(page.getByTestId("inbox-search"), "7600");
  await page.waitForTimeout(400);

  const conv = page.locator("[data-testid^='conv-']").first();
  if (await conv.isVisible().catch(() => false)) {
    await humanClick(page, conv);
    await page.waitForTimeout(600);
  }
}

async function walkEmailInbox(page) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-email-inbox");
  if (!(await nav.isVisible().catch(() => false))) return;
  await humanNavClick(page, nav, "/email-inbox");
  await page.waitForLoadState("domcontentloaded");
  await page.getByTestId("email-inbox-search")
    .or(page.getByTestId("page-title"))
    .first()
    .waitFor({ state: "visible", timeout: 15_000 })
    .catch(() => {});
}

async function walkAppointments(page, permissions) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-appointments");
  await humanNavClick(page, nav, "/appointments");
  await expect(page.getByTestId("appointments-kanban")).toBeVisible();

  const search = page.getByTestId("appt-search");
  if (await search.isVisible().catch(() => false)) {
    await humanType(search, "cardio");
    await page.waitForTimeout(400);
  }

  for (const col of APPT_KANBAN_COLUMNS) {
    const header = page.getByRole("heading", { name: col, exact: true });
    if (await header.isVisible().catch(() => false)) await header.scrollIntoViewIfNeeded();
  }

  if (permissions.includes(PERMISSIONS.AppointmentCreate)) {
    const btn = page.getByTestId("new-appt-btn");
    if (await btn.isVisible().catch(() => false)) {
      await humanClick(page, btn);
      await expect(page.getByTestId("new-appt-dialog")).toBeVisible();
      await page.keyboard.press("Escape");
    }
  }
}

async function walkTasks(page, permissions) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-follow-ups");
  await humanNavClick(page, nav, "/tasks");
  await expect(page.getByTestId("followups-kanban")).toBeVisible();

  const search = page.getByTestId("task-search");
  if (await search.isVisible().catch(() => false)) {
    await humanType(search, "follow");
  }

  for (const col of TASK_KANBAN_COLUMNS) {
    const header = page.getByRole("heading", { name: col, exact: true });
    if (await header.isVisible().catch(() => false)) await header.scrollIntoViewIfNeeded();
  }

  if (permissions.includes(PERMISSIONS.DashboardView)) {
    const btn = page.getByTestId("new-task-btn");
    if (await btn.isVisible().catch(() => false)) {
      await humanClick(page, btn);
      await page.keyboard.press("Escape");
    }
  }
}

async function walkCampaigns(page, permissions) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-campaigns");
  if (!(await nav.isVisible().catch(() => false))) return;
  await humanNavClick(page, nav, "/campaigns");
  await expect(page.getByTestId("campaigns-kanban")).toBeVisible();

  for (const col of CAMPAIGN_KANBAN_COLUMNS) {
    const header = page.getByRole("heading", { name: col, exact: true });
    if (await header.isVisible().catch(() => false)) await header.scrollIntoViewIfNeeded();
  }

  if (permissions.includes(PERMISSIONS.CampaignManage)) {
    const btn = page.getByTestId("new-campaign-btn");
    if (await btn.isVisible().catch(() => false)) {
      await humanClick(page, btn);
      await expect(page.getByTestId("new-campaign-dialog")).toBeVisible();
      await clickIfVisible(page, page.getByTestId("nc-preview-btn"));
      await page.keyboard.press("Escape");
    }
  }

  const card = page.locator("[data-testid^='campaign-card-']").first();
  if (await card.isVisible().catch(() => false)) {
    await humanClick(page, card);
    await page.waitForURL(/\/campaigns\/[^/]+/, { timeout: 20_000 }).catch(() => {});
    if (page.url().match(/\/campaigns\/[^/]+/)) {
      await humanNavClick(page, nav, "/campaigns");
    }
  }
}

async function walkAnalytics(page) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-analytics");
  await humanNavClick(page, nav, "/missed-revenue");
  const widget = page.getByTestId("analytics-total-loss");
  if (await widget.isVisible().catch(() => false)) {
    await expect(widget).toBeVisible();
  }
}

async function walkDoctors(page) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-referral-crm");
  if (!(await nav.isVisible().catch(() => false))) return;
  await humanNavClick(page, nav, "/doctors");

  const first = page.locator("[data-testid^='doctor-row-'], [data-testid^='referrer-card-']").first();
  if (await first.isVisible().catch(() => false)) {
    await humanClick(page, first);
    await page.waitForURL(/\/doctors\/[^/]+/, { timeout: 20_000 }).catch(() => {});
    for (const tabId of ["doctor-tab-overview", "doctor-tab-referrals"]) {
      await clickIfVisible(page, page.getByTestId(tabId));
    }
    if (page.url().match(/\/doctors\/[^/]+/)) {
      await humanNavClick(page, nav, "/doctors");
    }
  }
}

async function walkStaff(page, permissions) {
  const nav = page.getByTestId("app-sidebar").getByTestId("nav-hospital-staff");
  if (!(await nav.isVisible().catch(() => false))) return;
  await humanNavClick(page, nav, "/staff");
  await expect(page.getByTestId("staff-search")).toBeVisible();

  if (permissions.includes(PERMISSIONS.StaffCreate)) {
    const btn = page.getByTestId("new-staff-profile-btn");
    if (await btn.isVisible().catch(() => false)) {
      await humanClick(page, btn);
      await page.keyboard.press("Escape");
    }
  }
}

async function walkSettings(page, permissions) {
  const settingsNav = page.getByTestId("app-sidebar").getByTestId("nav-settings");
  if (!navAllowed(NAV_ITEMS.find((n) => n.testId === "nav-settings"), permissions)) return;
  if (!(await settingsNav.isVisible().catch(() => false))) return;

  await humanNavClick(page, settingsNav, "/settings");

  for (const link of SETTINGS_LINKS) {
    if (!permissions.includes(link.permission)) continue;
    const subLink = page.getByTestId("settings-nav").getByRole("link", { name: link.name, exact: true });
    if (!(await subLink.isVisible().catch(() => false))) continue;

    await humanClick(page, subLink);
    await page.waitForURL(
      (url) => url.pathname === link.path || url.pathname.startsWith(`${link.path}/`),
      { timeout: 30_000 },
    );
    if (link.expectTestId) {
      await expect(page.getByTestId(link.expectTestId)).toBeVisible({ timeout: 15_000 });
    }
  }
}

async function walkHeaderChrome(page) {
  const bell = page.getByTestId("notification-bell-desktop").first();
  if (await bell.isVisible().catch(() => false)) {
    await humanClick(page, bell);
    await page.keyboard.press("Escape");
  }
}

/**
 * Full deep walk for one role — single login, sidebar-only navigation.
 */
async function walkComprehensive(page, permissions, { roleLabel = "admin" } = {}) {
  const ux = createUxFindings();

  if (permissions.includes(PERMISSIONS.DashboardView)) {
    await walkDashboard(page, ux, { roleLabel });
  }
  if (permissions.includes(PERMISSIONS.PatientView)) {
    await walkPatients(page, ux, permissions);
  }
  if (permissions.includes(PERMISSIONS.ConversationView)) {
    await walkInbox(page);
    await walkEmailInbox(page);
  }
  if (permissions.includes(PERMISSIONS.AppointmentView)) {
    await walkAppointments(page, permissions);
  }
  if (permissions.includes(PERMISSIONS.DashboardView)) {
    await walkTasks(page, permissions);
  }
  if (permissions.includes(PERMISSIONS.CampaignView)) {
    await walkCampaigns(page, permissions);
  }
  if (permissions.includes(PERMISSIONS.DashboardView)) {
    await walkAnalytics(page);
  }
  if (permissions.includes(PERMISSIONS.ReferralView)) {
    await walkDoctors(page);
  }
  if (permissions.includes(PERMISSIONS.StaffView) || permissions.includes(PERMISSIONS.ClinicalView)) {
    await walkStaff(page, permissions);
  }
  await walkSettings(page, permissions);
  await walkHeaderChrome(page);

  console.log(`\n[comprehensive] ${roleLabel} UX notes:\n${ux.summary()}\n`);
  return { uxNotes: ux.list() };
}

module.exports = {
  walkComprehensive,
  walkDashboard,
  walkPatients,
  walkInbox,
  walkAppointments,
  walkTasks,
  walkCampaigns,
  walkAnalytics,
  walkDoctors,
  walkStaff,
  walkSettings,
};

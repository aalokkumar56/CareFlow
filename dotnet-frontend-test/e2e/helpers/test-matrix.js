/**
 * Central E2E test matrix — all routes from App.js, role/viewport/filter dimensions,
 * and scenario counting for TEST_MATRIX.md.
 */
const fs = require("fs");
const path = require("path");
const { PERMISSIONS } = require("../../../dotnet-frontend/src/lib/permissions");
const {
  MAIN_ROUTES,
  SETTINGS_ROUTES,
  NAV_ITEMS,
  FEATURE_BUTTONS,
  ALL_ROLES_MATRIX,
  PATIENT_TABS,
  PATIENT_EDIT_TABS,
} = require("./routes");

/** Every static route declared in src/App.js (excluding :id params). */
const STATIC_APP_ROUTES = [
  { path: "/login", label: "Login", public: true },
  { path: "/", label: "Dashboard", permission: PERMISSIONS.DashboardView, expectTestId: "page-title", apiPath: "/dashboard/overview" },
  { path: "/inbox", label: "WhatsApp Inbox", permission: PERMISSIONS.ConversationView, expectTestId: "inbox-search", apiPath: "/conversations" },
  { path: "/email-inbox", label: "Email Inbox", permission: PERMISSIONS.ConversationView, apiPath: "/email/threads" },
  { path: "/patients", label: "Patients", permission: PERMISSIONS.PatientView, expectTestId: "patients-search", apiPath: "/patients" },
  { path: "/appointments", label: "Appointments", permission: PERMISSIONS.AppointmentView, expectTestId: "appointments-kanban", apiPath: "/appointments" },
  { path: "/tasks", label: "Follow-ups", permission: PERMISSIONS.DashboardView, expectTestId: "followups-kanban", apiPath: "/tasks" },
  { path: "/doctors", label: "Referral CRM", permission: PERMISSIONS.ReferralView, apiPath: "/doctors" },
  { path: "/staff", label: "Hospital Staff", anyPermission: [PERMISSIONS.StaffView, PERMISSIONS.ClinicalView], expectTestId: "staff-search", apiPath: "/staff" },
  { path: "/campaigns", label: "Campaigns", permission: PERMISSIONS.CampaignView, expectTestId: "campaigns-kanban", apiPath: "/campaigns" },
  { path: "/missed-revenue", label: "Analytics", permission: PERMISSIONS.DashboardView, expectTestId: "analytics-total-loss", apiPath: "/dashboard/missed-revenue" },
  { path: "/settings", label: "Settings", permission: PERMISSIONS.SettingsView },
  { path: "/settings/users", label: "Settings Users", permission: PERMISSIONS.UserView, expectTestId: "users-search", apiPath: "/users" },
  { path: "/settings/roles", label: "Settings Roles", permission: PERMISSIONS.UserView, apiPath: "/roles" },
  { path: "/settings/permissions", label: "Settings Permissions", permission: PERMISSIONS.UserView, apiPath: "/permissions" },
  { path: "/settings/templates", label: "Settings Templates", permission: PERMISSIONS.SettingsView, apiPath: "/templates" },
  { path: "/notifications/preferences", label: "Notification Preferences", permission: null, expectTestId: "notification-preferences-title", apiPath: "/notification-preferences" },
  { path: "/settings/notifications", label: "Settings Notifications", permission: PERMISSIONS.SettingsView, apiPath: "/notification-preferences" },
  { path: "/settings/hospital", label: "Settings Hospital", permission: PERMISSIONS.SettingsView, apiPath: "/hospital" },
  { path: "/settings/integrations", label: "Settings Integrations", permission: PERMISSIONS.SettingsView, apiPath: "/integrations" },
];

/** Dynamic detail routes (resolved at runtime with seeded IDs). */
const DETAIL_ROUTE_SEEDS = [
  { pathTemplate: "/patients/:id", label: "Patient Detail", permission: PERMISSIONS.PatientView, expectTestId: "patient-name", apiPath: "/patients", seedKey: "patientId" },
  { pathTemplate: "/doctors/:id", label: "Doctor Detail", permission: PERMISSIONS.ReferralView, expectTestId: "doctor-name", apiPath: "/doctors", seedKey: "doctorId" },
  { pathTemplate: "/campaigns/:id", label: "Campaign Detail", permission: PERMISSIONS.CampaignView, seedKey: "campaignId", apiPath: "/campaigns" },
];

const VIEWPORTS = [
  { name: "desktop", width: 1280, height: 720 },
  { name: "tablet", width: 768, height: 1024 },
  { name: "mobile", width: 390, height: 844 },
];

const PATIENT_STATUS_FILTERS = [
  "All Statuses",
  "New Inquiry",
  "Contacted",
  "Visited",
  "Follow-up Pending",
  "No Response",
];
const PATIENT_DEPT_FILTERS_FALLBACK = ["All Departments", "Cardiology", "Orthopedics"];

/** Loaded from global-setup cache of GET /hospital-profile/departments when available. */
function loadPatientDeptFilters() {
  try {
    const cached = JSON.parse(
      fs.readFileSync(path.join(__dirname, ".e2e-departments.json"), "utf8"),
    );
    if (Array.isArray(cached) && cached.length) {
      return ["All Departments", ...cached];
    }
  } catch {
    /* global-setup not run yet */
  }
  return PATIENT_DEPT_FILTERS_FALLBACK;
}

const PATIENT_DEPT_FILTERS = loadPatientDeptFilters();
const PATIENT_SOURCE_FILTERS = [
  "All Sources",
  "WhatsApp",
  "Manual Entry",
  "Referral",
  "Website Form",
  "Phone Call",
];

const SEARCH_QUERIES = [
  "",
  "test",
  "e2e",
  "patient",
  "999",
  "cardio",
  "vip",
  "john",
  "91",
  "xyz-no-match-",
  "filter",
  "dashboard",
];

const LIST_SEARCH_PAGES = [
  { path: "/patients", testId: "patients-search", apiFragment: "/api/patients" },
  { path: "/appointments", testId: "appt-search", apiFragment: "/api/appointments" },
  { path: "/tasks", testId: "task-search", apiFragment: "/api/tasks" },
  { path: "/staff", testId: "staff-search", apiFragment: "/api/staff" },
  { path: "/inbox", testId: "inbox-search", apiFragment: "/api/conversations" },
  { path: "/settings/users", testId: "users-search", apiFragment: "/api/users" },
];

const DASHBOARD_APPT_PERIODS = ["this_week", "last_week"];
const DASHBOARD_REVENUE_PERIODS = ["this_month", "last_month", "this_week"];

const KANBAN_PAGES = [
  { path: "/appointments", testId: "appointments-kanban", columns: ["Scheduled", "Confirmed", "Completed", "Cancelled", "No-show"] },
  { path: "/tasks", testId: "followups-kanban", columns: ["Pending", "In Progress", "Completed", "Overdue"] },
  { path: "/campaigns", testId: "campaigns-kanban", columns: ["Draft", "Scheduled", "Active", "Completed"] },
];

const DOCTOR_DETAIL_TABS = [
  { testId: "doctor-tab-overview", label: "Overview" },
  { testId: "doctor-tab-referrals", label: "Referrals" },
];

const CRUD_DIALOGS = [
  { path: "/patients", btn: "new-patient-btn", dialog: "new-patient-dialog", permission: PERMISSIONS.PatientCreate },
  { path: "/appointments", btn: "new-appt-btn", dialog: "new-appt-dialog", permission: PERMISSIONS.AppointmentCreate },
  { path: "/campaigns", btn: "new-campaign-btn", dialog: "new-campaign-dialog", permission: PERMISSIONS.CampaignManage },
  { path: "/settings/users", btn: "new-user-btn", dialog: null, permission: PERMISSIONS.UserCreate },
  { path: "/tasks", btn: "new-task-btn", dialog: null, permission: PERMISSIONS.DashboardView },
  { path: "/staff", btn: "new-staff-profile-btn", dialog: null, permission: PERMISSIONS.StaffCreate },
];

const INVALID_LOGIN_CASES = [
  { email: "", password: "", label: "empty-fields" },
  { email: "not-an-email", password: "x", label: "invalid-email" },
  { email: "admin@cureflow.in", password: "wrong-password-xyz", label: "wrong-password" },
  { email: "missing@cureflow.test", password: "TestPass123!", label: "unknown-user" },
  { email: "admin@cureflow.in", password: "", label: "empty-password" },
];

const EMPTY_SEARCH_PAGES = [
  { path: "/patients", testId: "patients-search", query: "zzz-e2e-no-results-xyz" },
  { path: "/appointments", testId: "appt-search", query: "zzz-e2e-no-appt-xyz" },
  { path: "/tasks", testId: "task-search", query: "zzz-e2e-no-task-xyz" },
  { path: "/staff", testId: "staff-search", query: "zzz-e2e-no-staff-xyz" },
  { path: "/inbox", testId: "inbox-search", query: "zzz-e2e-no-conv-xyz" },
  { path: "/settings/users", testId: "users-search", query: "zzz-e2e-no-user-xyz" },
  { path: "/doctors", testId: null, query: "zzz-e2e-no-doctor-xyz" },
  { path: "/campaigns", testId: null, query: "zzz-e2e-no-campaign-xyz" },
];

const DASHBOARD_WIDGETS = [
  "dashboard-appointments-overview",
  "dashboard-appt-chart",
  "dashboard-upcoming-appointments",
  "dashboard-revenue-overview",
  "dashboard-revenue-chart",
  "dashboard-ai-insights",
];

const SETTINGS_SUB_ROUTES = SETTINGS_ROUTES;

function routeAllowed(route, permissions) {
  if (route.public) return true;
  if (route.anyPermission) {
    return route.anyPermission.some((p) => permissions.includes(p));
  }
  if (route.permission) return permissions.includes(route.permission);
  return true;
}

function protectedStaticRoutes() {
  return STATIC_APP_ROUTES.filter((r) => !r.public);
}

/** Count generated Playwright test scenarios across matrix spec files. */
function countMatrixScenarios() {
  const roles = ALL_ROLES_MATRIX.length;
  const viewports = VIEWPORTS.length;
  const protectedRoutes = protectedStaticRoutes().length;
  const detailRoutes = DETAIL_ROUTE_SEEDS.length;

  const buckets = {
    "navigation-matrix": protectedRoutes * roles * viewports + detailRoutes * roles * viewports,
    "api-assertions-matrix": (protectedRoutes + detailRoutes) * roles,
    "patients-matrix":
      PATIENT_STATUS_FILTERS.length * roles
      + PATIENT_DEPT_FILTERS.length * roles
      + PATIENT_SOURCE_FILTERS.length * roles
      + SEARCH_QUERIES.length * roles
      + PATIENT_TABS.length * roles
      + PATIENT_EDIT_TABS.length * roles,
    "appointments-matrix": SEARCH_QUERIES.length * roles + KANBAN_PAGES[0].columns.length * roles + viewports * roles,
    "dashboard-matrix":
      DASHBOARD_APPT_PERIODS.length * roles
      + DASHBOARD_REVENUE_PERIODS.length * roles
      + DASHBOARD_WIDGETS.length * roles,
    "analytics-matrix": 4 * roles + viewports * roles,
    "settings-matrix": SETTINGS_SUB_ROUTES.length * roles * viewports,
    "referrals-matrix": SEARCH_QUERIES.length * roles + DOCTOR_DETAIL_TABS.length * roles + viewports * roles,
    "inbox-matrix": SEARCH_QUERIES.length * roles * 2 + viewports * roles * 2,
    "campaigns-matrix": KANBAN_PAGES[2].columns.length * roles + viewports * roles,
    "tasks-matrix": SEARCH_QUERIES.length * roles + KANBAN_PAGES[1].columns.length * roles,
    "staff-matrix": SEARCH_QUERIES.length * roles + viewports * roles,
    "auth-matrix": INVALID_LOGIN_CASES.length + roles * 2 + protectedRoutes,
    "empty-states-matrix": EMPTY_SEARCH_PAGES.length * roles,
    "crud-dialogs-matrix": CRUD_DIALOGS.length * roles,
    "pagination-matrix": LIST_SEARCH_PAGES.length * 3 * roles,
    "notifications-matrix": roles * 4,
    "shell-matrix": require("./routes").APPSHELL_PAGES.length * roles * viewports,
  };

  const total = Object.values(buckets).reduce((sum, n) => sum + n, 0);
  return { buckets, total, roles, viewports, protectedRoutes, detailRoutes };
}

module.exports = {
  STATIC_APP_ROUTES,
  DETAIL_ROUTE_SEEDS,
  VIEWPORTS,
  PATIENT_STATUS_FILTERS,
  PATIENT_DEPT_FILTERS,
  PATIENT_SOURCE_FILTERS,
  SEARCH_QUERIES,
  LIST_SEARCH_PAGES,
  DASHBOARD_APPT_PERIODS,
  DASHBOARD_REVENUE_PERIODS,
  KANBAN_PAGES,
  DOCTOR_DETAIL_TABS,
  CRUD_DIALOGS,
  INVALID_LOGIN_CASES,
  EMPTY_SEARCH_PAGES,
  DASHBOARD_WIDGETS,
  MAIN_ROUTES,
  SETTINGS_ROUTES,
  SETTINGS_SUB_ROUTES,
  NAV_ITEMS,
  FEATURE_BUTTONS,
  ALL_ROLES_MATRIX,
  PATIENT_TABS,
  PATIENT_EDIT_TABS,
  routeAllowed,
  protectedStaticRoutes,
  countMatrixScenarios,
};

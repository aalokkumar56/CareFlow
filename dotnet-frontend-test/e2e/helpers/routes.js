/** All app routes with permission requirements and page expectations. */
const { PERMISSIONS } = require("../../../dotnet-frontend/src/lib/permissions");

const MAIN_ROUTES = [
  { path: "/", label: "Dashboard", permission: PERMISSIONS.DashboardView, expectTestId: "page-title" },
  { path: "/appointments", label: "Appointments", permission: PERMISSIONS.AppointmentView, expectTestId: "appointments-kanban" },
  { path: "/patients", label: "Patients", permission: PERMISSIONS.PatientView, expectTestId: "patients-search" },
  { path: "/inbox", label: "WhatsApp Inbox", permission: PERMISSIONS.ConversationView, expectTestId: "inbox-search" },
  { path: "/email-inbox", label: "Email Inbox", permission: PERMISSIONS.ConversationView },
  { path: "/campaigns", label: "Campaigns", permission: PERMISSIONS.CampaignView, expectTestId: "campaigns-kanban" },
  { path: "/missed-revenue", label: "Analytics", permission: PERMISSIONS.DashboardView },
  { path: "/tasks", label: "Follow-ups", permission: PERMISSIONS.DashboardView, expectTestId: "followups-kanban" },
  { path: "/doctors", label: "Referral CRM", permission: PERMISSIONS.ReferralView },
  { path: "/staff", label: "Hospital Staff", anyPermission: [PERMISSIONS.StaffView, PERMISSIONS.ClinicalView], expectTestId: "staff-search" },
];

const SETTINGS_ROUTES = [
  { path: "/settings/users", label: "Settings Users", permission: PERMISSIONS.UserView, expectTestId: "users-search" },
  { path: "/settings/roles", label: "Settings Roles", permission: PERMISSIONS.UserView },
  { path: "/settings/permissions", label: "Settings Permissions", permission: PERMISSIONS.UserView },
  { path: "/settings/templates", label: "Settings Templates", permission: PERMISSIONS.SettingsView },
  { path: "/settings/notifications", label: "Settings Notifications", permission: PERMISSIONS.SettingsView },
  { path: "/settings/hospital", label: "Settings Hospital", permission: PERMISSIONS.SettingsView },
  { path: "/settings/integrations", label: "Settings Integrations", permission: PERMISSIONS.SettingsView },
  { path: "/notifications/preferences", label: "Notification Preferences", permission: null, expectTestId: "notification-preferences-title" },
];

const PATIENT_TABS = [
  "tab-today",
  "tab-visit-chart",
  "tab-allergies",
  "tab-prescriptions",
  "tab-vitals",
  "tab-notes",
  "tab-history",
  "tab-lifestyle",
  "tab-timeline",
  "tab-details",
];

const CLINICAL_TABS = [
  "tab-visit-chart",
  "tab-allergies",
  "tab-prescriptions",
  "tab-vitals",
  "tab-notes",
  "tab-history",
  "tab-lifestyle",
];

const NAV_ITEMS = [
  { testId: "nav-dashboard", path: "/", permission: PERMISSIONS.DashboardView },
  { testId: "nav-appointments", path: "/appointments", permission: PERMISSIONS.AppointmentView },
  { testId: "nav-patients", path: "/patients", permission: PERMISSIONS.PatientView },
  { testId: "nav-whatsapp-inbox", path: "/inbox", permission: PERMISSIONS.ConversationView },
  { testId: "nav-email-inbox", path: "/email-inbox", permission: PERMISSIONS.ConversationView },
  { testId: "nav-campaigns", path: "/campaigns", permission: PERMISSIONS.CampaignView },
  { testId: "nav-analytics", path: "/missed-revenue", permission: PERMISSIONS.DashboardView },
  { testId: "nav-follow-ups", path: "/tasks", permission: PERMISSIONS.DashboardView },
  { testId: "nav-referral-crm", path: "/doctors", permission: PERMISSIONS.ReferralView },
  { testId: "nav-hospital-staff", path: "/staff", anyPermission: [PERMISSIONS.StaffView, PERMISSIONS.ClinicalView] },
  { testId: "nav-settings", path: "/settings", permission: PERMISSIONS.SettingsView },
];

const PATIENT_EDIT_TABS = ["edit-tab-basic", "edit-tab-personal", "edit-tab-address", "edit-tab-emergency"];

const FEATURE_BUTTONS = [
  { path: "/patients", testId: "new-patient-btn", permission: PERMISSIONS.PatientCreate, label: "New Patient" },
  { path: "/appointments", testId: "new-appt-btn", permission: PERMISSIONS.AppointmentCreate, label: "New Appointment" },
  { path: "/campaigns", testId: "new-campaign-btn", permission: PERMISSIONS.CampaignManage, label: "New Campaign" },
  { path: "/settings/users", testId: "new-user-btn", permission: PERMISSIONS.UserCreate, label: "New User" },
  { path: "/staff", testId: "new-staff-profile-btn", permission: PERMISSIONS.StaffCreate, label: "New Staff" },
  { path: "/tasks", testId: "new-task-btn", permission: PERMISSIONS.DashboardView, label: "New Task" },
];

/** Pages using AppShell hideHeaderSearch / hideNotifications — documents expected shell chrome. */
const APPSHELL_PAGES = [
  { path: "/", label: "Dashboard", hideHeaderSearch: false, hideNotifications: false },
  { path: "/patients", label: "Patients", hideHeaderSearch: true, hideNotifications: false },
  { path: "/appointments", label: "Appointments", hideHeaderSearch: true, hideNotifications: false },
  { path: "/tasks", label: "Tasks", hideHeaderSearch: true, hideNotifications: false },
  { path: "/campaigns", label: "Campaigns", hideHeaderSearch: true, hideNotifications: false },
  { path: "/staff", label: "Staff", hideHeaderSearch: true, hideNotifications: false },
  { path: "/doctors", label: "Doctors", hideHeaderSearch: true, hideNotifications: false },
  { path: "/inbox", label: "Inbox", hideHeaderSearch: true, hideNotifications: false },
  { path: "/email-inbox", label: "Email Inbox", hideHeaderSearch: true, hideNotifications: false },
  { path: "/missed-revenue", label: "Analytics", hideHeaderSearch: true, hideNotifications: false },
  { path: "/settings/users", label: "Settings Users", hideHeaderSearch: true, hideNotifications: false },
];

const ALL_ROLES_MATRIX = ["admin", "doctor", "reception", "nurse", "marketing", "staff"];

const DESIGN_AUDIT_PAGES = [
  "/",
  "/patients",
  "/appointments",
  "/tasks",
  "/campaigns",
  "/staff",
  "/doctors",
  "/inbox",
  "/email-inbox",
  "/missed-revenue",
  "/settings/users",
  "/settings/roles",
  "/settings/permissions",
  "/settings/templates",
  "/settings/notifications",
  "/settings/hospital",
  "/settings/integrations",
];

module.exports = {
  MAIN_ROUTES,
  SETTINGS_ROUTES,
  PATIENT_TABS,
  CLINICAL_TABS,
  PATIENT_EDIT_TABS,
  NAV_ITEMS,
  FEATURE_BUTTONS,
  APPSHELL_PAGES,
  ALL_ROLES_MATRIX,
  DESIGN_AUDIT_PAGES,
};

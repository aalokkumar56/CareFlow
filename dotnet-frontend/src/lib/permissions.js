/** Canonical permission codes — mirrors CureFlowPermissions.cs */
export const PERMISSIONS = {
  PatientView: "Patient.View",
  PatientCreate: "Patient.Create",
  PatientEdit: "Patient.Edit",
  PatientDelete: "Patient.Delete",
  AppointmentView: "Appointment.View",
  AppointmentCreate: "Appointment.Create",
  AppointmentEdit: "Appointment.Edit",
  AppointmentDelete: "Appointment.Delete",
  BillingView: "Billing.View",
  BillingCreate: "Billing.Create",
  BillingEdit: "Billing.Edit",
  BillingDelete: "Billing.Delete",
  UserView: "User.View",
  UserCreate: "User.Create",
  UserEdit: "User.Edit",
  UserDelete: "User.Delete",
  WhatsAppView: "WhatsApp.View",
  WhatsAppSend: "WhatsApp.Send",
  WhatsAppManage: "WhatsApp.Manage",
  ConversationView: "Conversation.View",
  ConversationManage: "Conversation.Manage",
  CampaignView: "Campaign.View",
  CampaignManage: "Campaign.Manage",
  ClinicalView: "Clinical.View",
  ClinicalEdit: "Clinical.Edit",
  DashboardView: "Dashboard.View",
  AuditView: "Audit.View",
  SettingsView: "Settings.View",
  SettingsEdit: "Settings.Edit",
  ReferralView: "Referral.View",
  ReferralManage: "Referral.Manage",
  StaffView: "Staff.View",
  StaffCreate: "Staff.Create",
  StaffEdit: "Staff.Edit",
  StaffDelete: "Staff.Delete",
};

/** Fallback when /auth/me has no permissions array (offline / stale session). */
export const ROLE_PERMISSIONS = {
  tenant_owner: Object.values(PERMISSIONS),
  admin: Object.values(PERMISSIONS),
  doctor: [
    PERMISSIONS.DashboardView, PERMISSIONS.PatientView, PERMISSIONS.PatientEdit,
    PERMISSIONS.AppointmentView, PERMISSIONS.AppointmentCreate, PERMISSIONS.AppointmentEdit,
    PERMISSIONS.ClinicalView, PERMISSIONS.ClinicalEdit, PERMISSIONS.ConversationView,
    PERMISSIONS.StaffView,
  ],
  reception: [
    PERMISSIONS.DashboardView, PERMISSIONS.PatientView, PERMISSIONS.PatientCreate, PERMISSIONS.PatientEdit,
    PERMISSIONS.AppointmentView, PERMISSIONS.AppointmentCreate, PERMISSIONS.AppointmentEdit,
    PERMISSIONS.ConversationView, PERMISSIONS.ConversationManage,
    PERMISSIONS.WhatsAppView, PERMISSIONS.WhatsAppSend,
    PERMISSIONS.StaffView,
  ],
  nurse: [
    PERMISSIONS.DashboardView, PERMISSIONS.PatientView,
    PERMISSIONS.AppointmentView,
    PERMISSIONS.ClinicalView, PERMISSIONS.ClinicalEdit,
    PERMISSIONS.StaffView,
  ],
  marketing: [
    PERMISSIONS.DashboardView, PERMISSIONS.PatientView,
    PERMISSIONS.CampaignView, PERMISSIONS.CampaignManage,
    PERMISSIONS.WhatsAppView, PERMISSIONS.WhatsAppSend,
  ],
  staff: [
    PERMISSIONS.DashboardView, PERMISSIONS.PatientView,
    PERMISSIONS.ConversationView, PERMISSIONS.ReferralView, PERMISSIONS.ReferralManage,
  ],
};

export const ROLE_LABELS = {
  tenant_owner: "Tenant Owner",
  admin: "Admin",
  doctor: "Doctor",
  reception: "Reception",
  receptionist: "Reception",
  nurse: "Nurse",
  marketing: "Marketing",
  staff: "Staff",
};

/** Roles accepted by POST /api/users (matches UserRole enum, snake_case JSON). */
export const API_USER_ROLES = [
  "admin",
  "doctor",
  "reception",
  "marketing",
  "staff",
  "nurse",
];

export const ALL_ROLES = ["tenant_owner", ...API_USER_ROLES];

export const normalizeRole = (role) => String(role || "").toLowerCase();

export const isAdminRole = (role) => {
  const r = normalizeRole(role);
  return r === "admin" || r === "tenant_owner";
};

export const getPermissionsForUser = (user) => {
  if (Array.isArray(user?.permissions) && user.permissions.length > 0) {
    return user.permissions;
  }
  return ROLE_PERMISSIONS[normalizeRole(user?.role)] || [];
};

export const hasPermission = (user, permission) => {
  const perms = getPermissionsForUser(user);
  return perms.includes(permission);
};

export const formatRole = (role) => ROLE_LABELS[normalizeRole(role)] || role || "—";

export const PERMISSION_LABELS = {
  [PERMISSIONS.PatientView]: "View patients",
  [PERMISSIONS.PatientCreate]: "Create patients",
  [PERMISSIONS.PatientEdit]: "Edit patients",
  [PERMISSIONS.PatientDelete]: "Delete patients",
  [PERMISSIONS.AppointmentView]: "View appointments",
  [PERMISSIONS.AppointmentCreate]: "Create appointments",
  [PERMISSIONS.AppointmentEdit]: "Edit appointments",
  [PERMISSIONS.AppointmentDelete]: "Delete appointments",
  [PERMISSIONS.BillingView]: "View billing",
  [PERMISSIONS.BillingCreate]: "Create billing records",
  [PERMISSIONS.BillingEdit]: "Edit billing records",
  [PERMISSIONS.BillingDelete]: "Delete billing records",
  [PERMISSIONS.UserView]: "View users",
  [PERMISSIONS.UserCreate]: "Create users",
  [PERMISSIONS.UserEdit]: "Edit users",
  [PERMISSIONS.UserDelete]: "Delete users",
  [PERMISSIONS.WhatsAppView]: "View WhatsApp",
  [PERMISSIONS.WhatsAppSend]: "Send WhatsApp messages",
  [PERMISSIONS.WhatsAppManage]: "Manage WhatsApp settings",
  [PERMISSIONS.ConversationView]: "View inbox",
  [PERMISSIONS.ConversationManage]: "Manage conversations",
  [PERMISSIONS.CampaignView]: "View campaigns",
  [PERMISSIONS.CampaignManage]: "Manage campaigns",
  [PERMISSIONS.ClinicalView]: "View clinical records",
  [PERMISSIONS.ClinicalEdit]: "Edit clinical records",
  [PERMISSIONS.DashboardView]: "View dashboard",
  [PERMISSIONS.AuditView]: "View audit log",
  [PERMISSIONS.SettingsView]: "View settings",
  [PERMISSIONS.SettingsEdit]: "Edit settings",
  [PERMISSIONS.ReferralView]: "View referral CRM",
  [PERMISSIONS.ReferralManage]: "Manage referrals",
  [PERMISSIONS.StaffView]: "View hospital staff",
  [PERMISSIONS.StaffCreate]: "Create staff profiles",
  [PERMISSIONS.StaffEdit]: "Edit staff profiles",
  [PERMISSIONS.StaffDelete]: "Delete staff profiles",
};

/** Decode permission claims from JWT when user object lacks permissions. */
export const getPermissionsFromToken = () => {
  try {
    const token = localStorage.getItem("cureflow_token");
    if (!token) return [];
    const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    const claims = payload.permission || payload.permissions || [];
    return Array.isArray(claims) ? claims : [claims].filter(Boolean);
  } catch {
    return [];
  }
};

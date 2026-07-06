import { PERMISSIONS, getPermissionsForUser } from "@/lib/permissions";

/** Routes to warm in the background after login — mirrors sidebar + settings nav. */
const APP_PREFETCH_ROUTES = [
  { path: "/", permission: PERMISSIONS.DashboardView },
  { path: "/appointments", permission: PERMISSIONS.AppointmentView },
  { path: "/patients", permission: PERMISSIONS.PatientView },
  { path: "/inbox", permission: PERMISSIONS.ConversationView },
  { path: "/email-inbox", permission: PERMISSIONS.ConversationView },
  { path: "/campaigns", permission: PERMISSIONS.CampaignView },
  { path: "/missed-revenue", permission: PERMISSIONS.DashboardView },
  { path: "/tasks", permission: PERMISSIONS.DashboardView },
  { path: "/doctors", permission: PERMISSIONS.ReferralView },
  {
    path: "/staff",
    anyPermission: [PERMISSIONS.StaffView, PERMISSIONS.ClinicalView],
  },
  { path: "/settings", permission: PERMISSIONS.SettingsView },
  { path: "/settings/users", permission: PERMISSIONS.UserView },
  { path: "/settings/roles", permission: PERMISSIONS.UserView },
  { path: "/settings/permissions", permission: PERMISSIONS.UserView },
  { path: "/settings/templates", permission: PERMISSIONS.SettingsView },
  { path: "/settings/notifications", permission: PERMISSIONS.SettingsView },
  { path: "/settings/hospital", permission: PERMISSIONS.SettingsView },
  { path: "/settings/integrations", permission: PERMISSIONS.SettingsView },
  { path: "/notifications/preferences", permission: null },
];

function routeAllowed(route, permissions) {
  if (route.anyPermission) {
    return route.anyPermission.some((p) => permissions.includes(p));
  }
  if (route.permission) return permissions.includes(route.permission);
  return true;
}

/** Paths this user may open — used for post-login prefetch. */
export function getPrefetchPathsForUser(user) {
  if (!user) return [];
  const permissions = getPermissionsForUser(user);
  return APP_PREFETCH_ROUTES.filter((route) => routeAllowed(route, permissions)).map(
    (route) => route.path,
  );
}

import { useMemo } from "react";
import { useAuth } from "@/lib/auth";
import {
  getPermissionsFromToken,
  hasPermission as checkPermission,
  isAdminRole,
  normalizeRole,
  ROLE_PERMISSIONS,
} from "@/lib/permissions";

export function usePermissions() {
  const { user } = useAuth();

  const permissions = useMemo(() => {
    if (!user) return [];
    const apiPerms = Array.isArray(user.permissions) ? user.permissions : [];
    const tokenPerms = getPermissionsFromToken();
    const merged = [...new Set([...apiPerms, ...tokenPerms])];
    if (merged.length > 0) return merged;
    return ROLE_PERMISSIONS[normalizeRole(user.role)] || [];
  }, [user]);

  const role = normalizeRole(user?.role);
  const isAdmin = isAdminRole(user?.role);

  const can = (permission) => {
    if (!permission) return true;
    if (permissions.includes(permission)) return true;
    return checkPermission(user, permission);
  };
  const canAny = (list) => list.some((p) => can(p));
  const canAll = (list) => list.every((p) => can(p));

  /** True when the user may call APIs protected by this permission. */
  const canFetch = (permission) => Boolean(user) && can(permission);

  return { user, role, permissions, isAdmin, can, canAny, canAll, canFetch };
}

export default usePermissions;

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { apiGet, apiPost, apiPut, toastApiError } from "@/lib/api";
import { PERMISSIONS } from "@/lib/permissions";
import usePermissions from "@/hooks/usePermissions";
import { toast } from "sonner";

const CATEGORY_ORDER = ["Inbox", "Appointments", "Tasks", "Campaigns", "Clinical", "Patients"];

const NOTIFICATION_TAB_TRIGGER =
  "rounded-lg data-[state=active]:bg-[#064E3B] data-[state=active]:text-white";

const NotificationPreferencesContent = ({ showRoleDefaults = null }) => {
  const { can } = usePermissions();
  const canEditRoles = showRoleDefaults ?? can(PERMISSIONS.SettingsEdit);
  const [prefs, setPrefs] = useState([]);
  const [roleDefaults, setRoleDefaults] = useState([]);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState("personal");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const personal = await apiGet("/notification-preferences");
      setPrefs(Array.isArray(personal) ? personal : []);
      if (canEditRoles) {
        const roles = await apiGet("/notification-preferences/role-defaults");
        setRoleDefaults(Array.isArray(roles) ? roles : []);
      }
    } catch (error) {
      toastApiError(error, "Failed to load notification settings");
    } finally {
      setLoading(false);
    }
  }, [canEditRoles]);

  useEffect(() => {
    load();
  }, [load]);

  const grouped = useMemo(() => {
    const map = new Map();
    prefs.forEach((p) => {
      if (!map.has(p.category)) map.set(p.category, []);
      map.get(p.category).push(p);
    });
    return CATEGORY_ORDER.filter((c) => map.has(c)).map((c) => ({ category: c, items: map.get(c) }));
  }, [prefs]);

  const togglePref = (type, enabled) => {
    setPrefs((prev) => prev.map((p) => (p.notification_type === type ? { ...p, in_app_enabled: enabled } : p)));
  };

  const savePersonal = async () => {
    try {
      await apiPut("/notification-preferences", {
        preferences: prefs.map((p) => ({
          notification_type: p.notification_type,
          in_app_enabled: p.in_app_enabled,
        })),
      });
      toast.success("Notification preferences saved");
    } catch (error) {
      toastApiError(error, "Failed to save preferences");
    }
  };

  const resetPersonal = async () => {
    try {
      await apiPost("/notification-preferences/reset");
      await load();
      toast.success("Reset to role defaults");
    } catch (error) {
      toastApiError(error, "Failed to reset preferences");
    }
  };

  const toggleRoleDefault = (roleId, notificationType, enabled) => {
    setRoleDefaults((prev) =>
      prev.map((r) =>
        r.role_id === roleId && r.notification_type === notificationType
          ? { ...r, in_app_enabled: enabled }
          : r,
      ),
    );
  };

  const saveRoleDefaults = async () => {
    try {
      await apiPut("/notification-preferences/role-defaults", roleDefaults);
      toast.success("Role defaults saved");
    } catch (error) {
      toastApiError(error, "Failed to save role defaults");
    }
  };

  const roles = useMemo(() => {
    const seen = new Map();
    roleDefaults.forEach((r) => {
      if (!seen.has(r.role_id)) seen.set(r.role_id, r.role_name);
    });
    return [...seen.entries()].map(([id, name]) => ({ id, name }));
  }, [roleDefaults]);

  const types = useMemo(() => {
    const seen = new Set();
    return roleDefaults.filter((r) => {
      if (seen.has(r.notification_type)) return false;
      seen.add(r.notification_type);
      return true;
    });
  }, [roleDefaults]);

  return (
    <div className="flex flex-col flex-1 min-h-0 h-full">
      <p className="text-ui-base text-text-secondary mb-5 shrink-0 leading-relaxed">
        Control which in-app alerts you receive. Access is limited by your role permissions.
      </p>

      {canEditRoles && (
        <Tabs value={tab} onValueChange={setTab} className="mb-5 shrink-0">
          <TabsList className="h-auto p-1 rounded-xl glass-card border-white/60 bg-white/30 w-full sm:w-auto flex flex-wrap gap-1">
            <TabsTrigger value="personal" className={NOTIFICATION_TAB_TRIGGER}>
              My preferences
            </TabsTrigger>
            <TabsTrigger value="roles" className={NOTIFICATION_TAB_TRIGGER}>
              Role defaults
            </TabsTrigger>
          </TabsList>
        </Tabs>
      )}

      <div
        data-testid="notifications-prefs-scroll"
        className="flex-1 min-h-0 overflow-y-auto scrollbar-thin"
      >
        {loading ? (
          <p className="text-ui-sm text-text-muted">Loading…</p>
        ) : tab === "roles" && canEditRoles ? (
          <div className="space-y-4">
            <p className="text-ui-sm text-text-muted">
              Set default notification enablement per role. Cells are disabled when the role lacks required permissions.
            </p>
            <div className="overflow-x-auto -mx-1 px-1">
              <table className="w-full text-ui-sm border-collapse min-w-[480px]">
                <thead>
                  <tr className="border-b border-white/40">
                    <th className="text-left py-2 pr-4 font-medium text-text-secondary">Type</th>
                    {roles.map((r) => (
                      <th key={r.id} className="text-center py-2 px-2 font-medium text-text-secondary whitespace-nowrap">
                        {r.name}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {types.map((row) => (
                    <tr key={row.notification_type} className="border-b border-white/20">
                      <td className="py-2 pr-4 text-[#022C22] capitalize">
                        {row.notification_type.split(".").pop().replace(/_/g, " ")}
                      </td>
                      {roles.map((role) => {
                        const cell = roleDefaults.find(
                          (d) => d.role_id === role.id && d.notification_type === row.notification_type,
                        );
                        if (!cell) return <td key={role.id} />;
                        return (
                          <td key={role.id} className="text-center py-2 px-2">
                            <Switch
                              checked={cell.in_app_enabled}
                              disabled={!cell.can_enable}
                              onCheckedChange={(v) => toggleRoleDefault(role.id, row.notification_type, v)}
                              aria-label={`${row.notification_type} for ${role.name}`}
                            />
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Button onClick={saveRoleDefaults} className="btn-primary rounded-xl h-9 text-[13px]">Save role defaults</Button>
          </div>
        ) : (
          <div className="space-y-6">
            {grouped.length === 0 ? (
              <p className="text-ui-base text-text-secondary">No configurable notification types for your role.</p>
            ) : (
              grouped.map(({ category, items }) => (
                <section
                  key={category}
                  className="rounded-xl border border-white/60 bg-white/25 backdrop-blur-md overflow-hidden"
                >
                  <div className="px-4 py-3 border-b border-white/50 bg-white/20 backdrop-blur-sm">
                    <h3 className="font-heading text-ui-base font-semibold text-[#022C22]">{category}</h3>
                  </div>
                  <div className="divide-y divide-white/40">
                    {items.map((p) => (
                      <div
                        key={p.notification_type}
                        className="flex items-center justify-between gap-4 px-4 py-3"
                      >
                        <div className="min-w-0">
                          <p className="text-ui-base font-medium text-[#022C22] capitalize">{p.label}</p>
                          {p.description && (
                            <p className="text-ui-sm text-text-secondary mt-0.5 leading-relaxed">{p.description}</p>
                          )}
                        </div>
                        <div className="flex items-center gap-2 shrink-0 min-h-[44px]">
                          <span className="text-ui-sm text-text-secondary hidden sm:inline">In-app</span>
                          <Switch
                            checked={p.in_app_enabled}
                            onCheckedChange={(v) => togglePref(p.notification_type, v)}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                </section>
              ))
            )}
            <div className="flex flex-wrap gap-2 pt-1">
              <Button onClick={savePersonal} className="btn-primary rounded-xl min-h-[44px] h-10 text-ui-base">Save preferences</Button>
              <Button variant="outline" onClick={resetPersonal} className="rounded-xl min-h-[44px] h-10 text-ui-base border-[#E5E7EB]">
                Reset to role defaults
              </Button>
            </div>
            <p className="text-ui-sm text-text-secondary pb-2">Email notifications — coming soon.</p>
          </div>
        )}
      </div>
    </div>
  );
};

export default NotificationPreferencesContent;

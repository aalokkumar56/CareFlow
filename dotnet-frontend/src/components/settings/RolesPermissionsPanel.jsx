import React, { useCallback, useEffect, useMemo, useState } from "react";
import GlassCard from "@/components/glass/GlassCard";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { api, normalizeApiError } from "@/lib/api";
import { Plus } from "@phosphor-icons/react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";

const FEATURE_ROWS = [
  { label: "Dashboard Access", permissions: ["Dashboard.View"] },
  { label: "Patient Management", permissions: ["Patient.View", "Patient.Create", "Patient.Edit"] },
  { label: "Appointments", permissions: ["Appointment.View", "Appointment.Create", "Appointment.Edit"] },
  { label: "Prescriptions", permissions: ["Clinical.View", "Clinical.Edit"] },
  { label: "Billing", permissions: ["Billing.View", "Billing.Create", "Billing.Edit"] },
  { label: "Reports", permissions: ["Dashboard.View", "Audit.View"] },
  { label: "User Management", permissions: ["User.View", "User.Create", "User.Edit"] },
  { label: "Settings", permissions: ["Settings.View", "Settings.Edit"] },
];

const PROTECTED = "SuperAdmin";

const toDraft = (roles) =>
  Object.fromEntries(roles.map((r) => [r.name, [...(r.permissions || [])]]));

const draftsEqual = (a, b) => {
  const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
  for (const key of keys) {
    const left = [...(a[key] || [])].sort().join("|");
    const right = [...(b[key] || [])].sort().join("|");
    if (left !== right) return false;
  }
  return true;
};

const featureState = (permSet, feature) => {
  const count = feature.permissions.filter((p) => permSet.has(p)).length;
  if (count === 0) return { checked: false, indeterminate: false };
  if (count === feature.permissions.length) return { checked: true, indeterminate: false };
  return { checked: false, indeterminate: true };
};

const RolesPermissionsPanel = ({ fillHeight = false }) => {
  const { can } = usePermissions();
  const canEdit = can(PERMISSIONS.UserEdit);
  const [permissions, setPermissions] = useState([]);
  const [roles, setRoles] = useState([]);
  const [draft, setDraft] = useState({});
  const [baseline, setBaseline] = useState({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [newRole, setNewRole] = useState({ name: "", description: "" });

  const load = useCallback(() => {
    setLoading(true);
    Promise.all([api.get("/admin/permissions"), api.get("/admin/roles")])
      .then(([permRes, roleRes]) => {
        const list = roleRes.data || [];
        setPermissions(permRes.data || []);
        setRoles(list);
        const next = toDraft(list);
        setDraft(next);
        setBaseline(next);
      })
      .catch((e) => toast.error(normalizeApiError(e, "Failed to load roles")))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { load(); }, [load]);

  const dirty = useMemo(() => !draftsEqual(draft, baseline), [draft, baseline]);

  const toggleFeature = (roleName, feature, enabled) => {
    if (roleName === PROTECTED) return;
    setDraft((prev) => {
      const set = new Set(prev[roleName] || []);
      feature.permissions.forEach((p) => (enabled ? set.add(p) : set.delete(p)));
      return { ...prev, [roleName]: [...set] };
    });
  };

  const discard = () => setDraft(baseline);

  const saveAll = async () => {
    const changed = roles.filter((r) => {
      const left = [...(draft[r.name] || [])].sort().join("|");
      const right = [...(baseline[r.name] || [])].sort().join("|");
      return left !== right;
    });
    if (changed.length === 0) return;

    setSaving(true);
    try {
      await Promise.all(
        changed.map((r) => api.put(`/admin/roles/${encodeURIComponent(r.name)}`, {
          permissions: draft[r.name] || [],
        })),
      );
      toast.success("Permissions saved");
      load();
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to save permissions"));
    } finally {
      setSaving(false);
    }
  };

  const createRole = async () => {
    if (!newRole.name.trim()) {
      toast.error("Role name required");
      return;
    }
    setSaving(true);
    try {
      await api.post("/admin/roles", {
        name: newRole.name,
        description: newRole.description,
        permissions: [],
      });
      toast.success("Role created — set permissions below and save");
      setCreateOpen(false);
      setNewRole({ name: "", description: "" });
      load();
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to create role"));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <GlassCard className={cn("text-center py-8 text-ui-sm text-text-muted", fillHeight && "flex-1 min-h-0 flex items-center justify-center")}>
        Loading roles…
      </GlassCard>
    );
  }

  return (
    <>
      <GlassCard
        padding={false}
        className={cn("overflow-hidden flex flex-col min-h-0", fillHeight && "flex-1")}
        data-testid="roles-permissions-panel"
      >
        <div className="px-3 py-2 border-b border-white/45 flex flex-wrap items-center justify-between gap-2 shrink-0">
          <div className="min-w-0">
            <h2 className="font-heading text-ui-base font-semibold text-[#022C22]">Roles &amp; Permissions</h2>
            <p className="text-ui-caption text-text-secondary truncate">
              Toggle checkboxes to grant access · save when ready
            </p>
          </div>
          {canEdit && (
            <div className="flex items-center gap-1.5 shrink-0">
              {dirty && (
                <>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="rounded-lg h-7 text-ui-caption px-2"
                    onClick={discard}
                    disabled={saving}
                  >
                    Discard
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    className="rounded-lg h-7 text-ui-caption btn-primary px-2"
                    onClick={saveAll}
                    disabled={saving}
                    data-testid="save-roles-btn"
                  >
                    {saving ? "Saving…" : "Save changes"}
                  </Button>
                </>
              )}
              <Button
                variant="outline"
                size="sm"
                className="rounded-lg h-7 text-ui-caption border-[#4338CA]/30 text-[#4338CA] hover:bg-sky-50 px-2"
                onClick={() => setCreateOpen(true)}
                data-testid="new-role-btn"
              >
                <Plus weight="bold" className="w-3 h-3 mr-0.5" />
                New Role
              </Button>
            </div>
          )}
        </div>

        <div className="flex-1 min-h-0 overflow-auto scrollbar-thin">
          <table className="w-full text-ui-sm min-w-[640px]">
            <thead className="sticky top-0 z-10">
              <tr className="border-b border-white/45 bg-white/70 backdrop-blur-sm">
                <th className="text-left px-3 py-2 font-semibold text-text-secondary w-[160px] align-bottom">Permission</th>
                {roles.map((r) => (
                  <th
                    key={r.name}
                    className="text-center px-2 py-2 font-semibold text-ui-caption text-text-secondary min-w-[88px] align-bottom"
                  >
                    <div className="font-semibold text-[#022C22]">{r.label || r.name}</div>
                    {r.description && (
                      <div className="font-normal text-[10px] text-text-muted leading-tight mt-0.5 line-clamp-2">
                        {r.description}
                      </div>
                    )}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {FEATURE_ROWS.map((row) => (
                <tr key={row.label} className="border-b border-white/30 last:border-0 hover:bg-white/25">
                  <td className="px-3 py-1.5 font-medium text-ui-sm text-[#022C22]">{row.label}</td>
                  {roles.map((r) => {
                    const permSet = new Set(draft[r.name] || []);
                    const { checked, indeterminate } = featureState(permSet, row);
                    const locked = !canEdit || r.name === PROTECTED;
                    return (
                      <td key={r.name} className="text-center px-2 py-1.5">
                        <Checkbox
                          checked={indeterminate ? "indeterminate" : checked}
                          disabled={locked}
                          onCheckedChange={(v) => toggleFeature(r.name, row, v === true)}
                          className="mx-auto h-3.5 w-3.5 data-[state=checked]:bg-[#4338CA] data-[state=checked]:border-[#4338CA] data-[state=indeterminate]:bg-[#4338CA]/80 data-[state=indeterminate]:border-[#4338CA]"
                        />
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {permissions.length > 0 && (
          <div className="px-3 py-1 border-t border-white/35 text-[10px] text-text-muted shrink-0">
            {roles.length} roles · {permissions.length} permissions
            {dirty ? " · unsaved changes" : ""}
          </div>
        )}
      </GlassCard>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>New Role</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Name</label>
              <Input
                data-testid="role-name-input"
                value={newRole.name}
                onChange={(e) => setNewRole({ ...newRole, name: e.target.value })}
                placeholder="Billing_Manager"
                className="rounded-xl"
              />
            </div>
            <div>
              <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Description</label>
              <Input
                value={newRole.description}
                onChange={(e) => setNewRole({ ...newRole, description: e.target.value })}
                placeholder="Optional"
                className="rounded-xl"
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setCreateOpen(false)} className="rounded-xl">Cancel</Button>
            <Button type="button" onClick={createRole} className="btn-primary rounded-xl" disabled={saving}>Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
};

export default RolesPermissionsPanel;

import React, { useCallback, useEffect, useState } from "react";
import SettingsLayout from "@/components/settings/SettingsLayout";
import SettingsNav from "@/components/settings/SettingsNav";
import RequirePermission from "@/components/RequirePermission";
import GlassCard from "@/components/glass/GlassCard";
import StatusPill from "@/components/glass/StatusPill";
import { api, normalizeApiError } from "@/lib/api";
import { PERMISSIONS, ALL_ROLES, API_USER_ROLES, formatRole } from "@/lib/permissions";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Plus, MagnifyingGlass, PencilSimple, Key, UserCircle } from "@phosphor-icons/react";
import { toast } from "sonner";

const emptyForm = {
  name: "", email: "", password: "", role: "reception",
  specialty: "", phone: "",
};

const toListRow = (user) => ({
  id: user.id,
  name: user.name,
  email: user.email,
  role: user.role,
  is_active: user.is_active !== false,
  specialty: user.specialty || null,
  phone: user.phone || null,
});

const UsersPage = () => (
  <RequirePermission permission={PERMISSIONS.UserView}>
    <UsersPageContent />
  </RequirePermission>
);

const UsersPageContent = () => {
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [q, setQ] = useState("");
  const [roleFilter, setRoleFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [createOpen, setCreateOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [form, setForm] = useState(emptyForm);
  const [editForm, setEditForm] = useState({ name: "", role: "reception", specialty: "", phone: "" });
  const [resetForm, setResetForm] = useState({ new_password: "", generate_temporary: true });
  const [tempPassword, setTempPassword] = useState("");

  const fetchUsers = useCallback(async (ensureUser = null) => {
    setLoading(true);
    const qs = new URLSearchParams({ limit: "500" });
    if (q) qs.append("q", q);
    if (roleFilter !== "all") qs.append("role", roleFilter);
    if (statusFilter !== "all") qs.append("is_active", statusFilter === "active" ? "true" : "false");
    try {
      const r = await api.get(`/users?${qs.toString()}`);
      let items = Array.isArray(r.data) ? r.data : [];
      if (ensureUser && !items.some((u) => u.id === ensureUser.id)) {
        items = [ensureUser, ...items];
      }
      setRows(items);
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to load users"));
    } finally {
      setLoading(false);
    }
  }, [q, roleFilter, statusFilter]);

  useEffect(() => { fetchUsers(); }, [fetchUsers]);

  const createUser = async () => {
    if (!form.name || !form.email || !form.password) {
      toast.error("Name, email, and password are required");
      return;
    }
    try {
      const res = await api.post("/users", form);
      toast.success("User created");
      setCreateOpen(false);
      setForm(emptyForm);
      await fetchUsers(toListRow(res.data));
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to create user"));
    }
  };

  const openEdit = (user) => {
    setSelected(user);
    setEditForm({
      name: user.name,
      role: user.role,
      specialty: user.specialty || "",
      phone: user.phone || "",
    });
    setEditOpen(true);
  };

  const saveEdit = async () => {
    if (!selected) return;
    try {
      await api.patch(`/users/${selected.id}`, editForm);
      toast.success("User updated");
      setEditOpen(false);
      fetchUsers();
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to update user"));
    }
  };

  const toggleActive = async (user) => {
    try {
      if (user.is_active) {
        await api.post(`/users/${user.id}/disable`);
        toast.success("User disabled");
      } else {
        await api.post(`/users/${user.id}/enable`);
        toast.success("User enabled");
      }
      fetchUsers();
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to update status"));
    }
  };

  const openReset = (user) => {
    setSelected(user);
    setResetForm({ new_password: "", generate_temporary: true });
    setTempPassword("");
    setResetOpen(true);
  };

  const resetPassword = async () => {
    if (!selected) return;
    try {
      const res = await api.post(`/users/${selected.id}/reset-password`, resetForm);
      setTempPassword(res.data.temporary_password);
      toast.success("Password reset");
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to reset password"));
    }
  };

  return (
    <SettingsLayout title="Users" sidebar={<SettingsNav />}>
      <div className="flex-1 min-w-0 flex flex-col gap-2 min-h-0 overflow-hidden">
            <div className="shrink-0 flex flex-col sm:flex-row gap-2 sm:items-center sm:justify-between">
              <div className="flex flex-wrap gap-2 flex-1">
                <div className="relative flex-1 min-w-[200px] max-w-sm">
                  <MagnifyingGlass className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-muted" />
                  <Input
                    value={q}
                    onChange={(e) => setQ(e.target.value)}
                    placeholder="Search name, email, phone..."
                    className="pl-9 rounded-xl"
                    data-testid="users-search"
                  />
                </div>
                <Select value={roleFilter} onValueChange={setRoleFilter}>
                  <SelectTrigger className="w-[150px] rounded-xl h-9"><SelectValue placeholder="Role" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All roles</SelectItem>
                    {ALL_ROLES.map((r) => (
                      <SelectItem key={r} value={r}>{formatRole(r)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Select value={statusFilter} onValueChange={setStatusFilter}>
                  <SelectTrigger className="w-[130px] rounded-xl h-9"><SelectValue placeholder="Status" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All status</SelectItem>
                    <SelectItem value="active">Active</SelectItem>
                    <SelectItem value="inactive">Disabled</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Dialog open={createOpen} onOpenChange={setCreateOpen}>
                <DialogTrigger asChild>
                  <Button className="btn-primary" data-testid="new-user-btn">
                    <Plus weight="bold" className="w-3.5 h-3.5 mr-1.5" /> Add user
                  </Button>
                </DialogTrigger>
                <DialogContent className="rounded-2xl max-w-md glass-card border-white/60">
                  <DialogHeader><DialogTitle className="font-heading">Add Team Member</DialogTitle></DialogHeader>
                  <UserFields form={form} setForm={setForm} includePassword />
                  <DialogFooter>
                    <Button variant="outline" onClick={() => setCreateOpen(false)} className="rounded-xl">Cancel</Button>
                    <Button onClick={createUser} className="btn-primary" data-testid="nu-save-btn">Create</Button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>
            </div>

            <GlassCard padding={false} className="flex-1 min-h-0 overflow-hidden flex flex-col">
              <div className="grid grid-cols-[1fr_1fr_120px_100px_120px] gap-2 px-3 py-2 border-b border-white/40 text-[10px] uppercase tracking-wider text-text-muted font-semibold hidden md:grid shrink-0">
                <span>User</span>
                <span>Contact</span>
                <span>Role</span>
                <span>Status</span>
                <span className="text-right">Actions</span>
              </div>
              {loading ? (
                <div className="px-3 py-8 text-center text-text-muted text-ui-sm">Loading...</div>
              ) : rows.length === 0 ? (
                <div className="px-3 py-8 text-center text-text-muted text-ui-sm">No users found</div>
              ) : (
                <div className="flex-1 min-h-0 overflow-y-auto scrollbar-thin">
                {rows.map((u) => (
                  <div
                    key={u.id}
                    className="px-3 py-2.5 border-b border-white/30 last:border-0 flex flex-col md:grid md:grid-cols-[1fr_1fr_120px_100px_120px] md:items-center gap-2 hover:bg-white/40 transition-colors"
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <div className="w-9 h-9 rounded-xl bg-[#064E3B]/10 text-[#064E3B] flex items-center justify-center shrink-0">
                        <UserCircle weight="duotone" className="w-5 h-5" />
                      </div>
                      <div className="min-w-0">
                        <div className="text-[13px] font-medium text-[#022C22] truncate">{u.name}</div>
                        <div className="text-[11px] text-text-muted font-mono truncate md:hidden">{u.email}</div>
                      </div>
                    </div>
                    <div className="text-[12px] text-text-secondary hidden md:block truncate">{u.email}</div>
                    <div>
                      <span className="text-[10px] uppercase tracking-wider px-2 py-0.5 rounded-lg bg-white/60 text-text-secondary">
                        {formatRole(u.role)}
                      </span>
                    </div>
                    <div>
                      <StatusPill
                        status={u.is_active ? "confirmed" : "lost"}
                        label={u.is_active ? "Active" : "Disabled"}
                      />
                    </div>
                    <div className="flex items-center gap-1 justify-end">
                      <Button variant="ghost" size="sm" className="h-8 w-8 p-0 rounded-xl" onClick={() => openEdit(u)} title="Edit" aria-label="Edit">
                        <PencilSimple className="w-4 h-4" />
                      </Button>
                      <Button variant="ghost" size="sm" className="h-8 w-8 p-0 rounded-xl" onClick={() => openReset(u)} title="Reset password" aria-label="Reset password">
                        <Key className="w-4 h-4" />
                      </Button>
                      <Switch checked={u.is_active} onCheckedChange={() => toggleActive(u)} />
                    </div>
                  </div>
                ))}
                </div>
              )}
            </GlassCard>
      </div>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="rounded-2xl max-w-md glass-card border-white/60">
          <DialogHeader><DialogTitle className="font-heading">Edit User</DialogTitle></DialogHeader>
          <UserFields form={editForm} setForm={setEditForm} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditOpen(false)} className="rounded-xl">Cancel</Button>
            <Button onClick={saveEdit} className="btn-primary">Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={resetOpen} onOpenChange={setResetOpen}>
        <DialogContent className="rounded-2xl max-w-md glass-card border-white/60">
          <DialogHeader>
            <DialogTitle className="font-heading">Reset Password</DialogTitle>
          </DialogHeader>
          <p className="text-[13px] text-text-secondary">
            Reset password for <strong>{selected?.name}</strong>
          </p>
          <label className="flex items-center gap-2 text-[13px]">
            <Switch
              checked={resetForm.generate_temporary}
              onCheckedChange={(c) => setResetForm({ ...resetForm, generate_temporary: c })}
            />
            Generate temporary password
          </label>
          {!resetForm.generate_temporary && (
            <div>
              <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">New password</label>
              <Input
                type="password"
                value={resetForm.new_password}
                onChange={(e) => setResetForm({ ...resetForm, new_password: e.target.value })}
                className="rounded-xl"
              />
            </div>
          )}
          {tempPassword && (
            <div className="bg-amber-50 border border-amber-200 rounded-xl p-3 text-[13px]">
              Temporary password: <code className="font-mono font-semibold">{tempPassword}</code>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setResetOpen(false)} className="rounded-xl">Close</Button>
            <Button onClick={resetPassword} className="btn-primary">Reset</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </SettingsLayout>
  );
};

const UserFields = ({ form, setForm, includePassword = false }) => (
  <div className="space-y-3">
    <Field label="Name" value={form.name} onChange={(v) => setForm({ ...form, name: v })} testId="nu-name" />
    {!includePassword && null}
    {includePassword && (
      <>
        <Field label="Email" value={form.email} onChange={(v) => setForm({ ...form, email: v })} testId="nu-email" />
        <Field label="Password" type="password" value={form.password} onChange={(v) => setForm({ ...form, password: v })} testId="nu-password" />
      </>
    )}
    <div>
      <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">Role</label>
      <Select value={form.role} onValueChange={(v) => setForm({ ...form, role: v })}>
        <SelectTrigger className="rounded-xl h-9" data-testid="nu-role"><SelectValue /></SelectTrigger>
        <SelectContent>
          {API_USER_ROLES.map((r) => (
            <SelectItem key={r} value={r}>{formatRole(r)}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
    <Field label="Phone" value={form.phone} onChange={(v) => setForm({ ...form, phone: v })} />
    {(form.role === "doctor" || includePassword) && (
      <Field label="Specialty" value={form.specialty} onChange={(v) => setForm({ ...form, specialty: v })} />
    )}
  </div>
);

const Field = ({ label, value, onChange, type, testId }) => (
  <div>
    <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">{label}</label>
    <Input
      data-testid={testId}
      type={type || "text"}
      value={value || ""}
      onChange={(e) => onChange(e.target.value)}
      className="rounded-xl"
    />
  </div>
);

export default UsersPage;

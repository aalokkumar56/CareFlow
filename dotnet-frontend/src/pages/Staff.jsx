import React, { useEffect, useState } from "react";
import AppShell from "@/components/layout/AppShell";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import ViewModeTabs from "@/components/glass/ViewModeTabs";
import StatusPill from "@/components/glass/StatusPill";
import { api, normalizeApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from "@/components/ui/dialog";
import {
  Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription,
} from "@/components/ui/sheet";
import { Plus, MagnifyingGlass, UserCircle, CalendarBlank } from "@phosphor-icons/react";
import FormField from "@/components/forms/FormField";
import DepartmentSelect from "@/components/forms/DepartmentSelect";
import useDepartments from "@/hooks/useDepartments";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";
import { toast } from "sonner";

const ROLE_LABELS = {
  doctor: "Doctor",
  nurse: "Nurse",
  staff: "Staff",
  reception: "Reception",
};

const EMPLOYMENT_LABELS = {
  permanent: "Permanent",
  visiting: "Visiting",
};

const TAB_MODES = [
  { id: "all", label: "All Staff" },
  { id: "doctors", label: "Doctors" },
  { id: "nurses", label: "Nurses" },
];

const DAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

const Staff = () => {
  const { departments } = useDepartments();
  const { can } = usePermissions();
  const canCreateStaff = can(PERMISSIONS.StaffCreate);
  const [tab, setTab] = useState("all");
  const [staff, setStaff] = useState([]);
  const [users, setUsers] = useState([]);
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [schedules, setSchedules] = useState([]);
  const [form, setForm] = useState({
    user_id: "",
    department: "",
    specialization: "",
    qualification: "",
    consultation_fee: "",
    employment_type: "permanent",
    shift: "",
    ward_assignment: "",
    is_available: true,
  });
  const [scheduleForm, setScheduleForm] = useState({
    day_of_week: "1",
    start_time: "09:00",
    end_time: "13:00",
    notes: "",
  });

  const load = () => {
    const endpoint = tab === "doctors" ? "/staff/doctors" : tab === "nurses" ? "/staff/nurses" : "/staff";
    const qs = q ? `?q=${encodeURIComponent(q)}` : "";
    api.get(`${endpoint}${qs}`).then((r) => setStaff(r.data)).catch(() => setStaff([]));
  };

  useEffect(() => {
    load();
    api.get("/users").then((r) => setUsers(r.data || [])).catch(() => setUsers([]));
    /* eslint-disable-next-line */
  }, [tab, q]);

  const openDetail = async (member) => {
    setSelected(member);
    if (member.role === "doctor") {
      try {
        const r = await api.get(`/staff/${member.id}/schedules`);
        setSchedules(r.data || []);
      } catch {
        setSchedules([]);
      }
    }
  };

  const createProfile = async () => {
    if (!form.user_id) { toast.error("Select a user"); return; }
    try {
      await api.post("/staff", {
        user_id: form.user_id,
        department: form.department || null,
        specialization: form.specialization || null,
        qualification: form.qualification || null,
        consultation_fee: form.consultation_fee ? parseFloat(form.consultation_fee) : null,
        employment_type: form.employment_type,
        shift: form.shift || null,
        ward_assignment: form.ward_assignment || null,
        is_available: form.is_available,
      });
      toast.success("Staff profile created");
      setOpen(false);
      load();
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to create profile"));
    }
  };

  const addSchedule = async () => {
    if (!selected) return;
    try {
      await api.post("/staff/schedules", {
        staff_profile_id: selected.id,
        day_of_week: parseInt(scheduleForm.day_of_week, 10),
        start_time: scheduleForm.start_time,
        end_time: scheduleForm.end_time,
        is_available: true,
        notes: scheduleForm.notes || null,
      });
      toast.success("Schedule added");
      const r = await api.get(`/staff/${selected.id}/schedules`);
      setSchedules(r.data || []);
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to add schedule"));
    }
  };

  const updateProfile = async () => {
    if (!selected) return;
    try {
      await api.patch(`/staff/${selected.id}`, {
        department: selected.department || null,
        specialization: selected.specialization || null,
        qualification: selected.qualification || null,
        consultation_fee: selected.consultation_fee ?? null,
        employment_type: selected.employment_type || "permanent",
        shift: selected.shift || null,
        ward_assignment: selected.ward_assignment || null,
        is_available: selected.is_available,
      });
      toast.success("Profile updated");
      load();
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to update profile"));
    }
  };

  const staffUsers = users.filter((u) =>
    ["doctor", "nurse", "staff", "reception"].includes(u.role)
  );

  return (
    <AppShell
      title="Hospital Staff"
      subtitle="Internal doctors, nurses & staff"
      hideHeaderSearch
      hideHospitalBadge
      showDate={false}
      scrollable={false}
      compactFooter
      wide
      actions={canCreateStaff ? (
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button className="btn-primary h-9 rounded-xl text-[13px]" data-testid="new-staff-profile-btn">
              <Plus weight="bold" className="w-3.5 h-3.5 mr-1.5" /> Add Staff Profile
            </Button>
          </DialogTrigger>
          <DialogContent className="rounded-2xl max-w-lg glass-card border-white/60">
            <DialogHeader>
              <DialogTitle>New Staff Profile</DialogTitle>
            </DialogHeader>
            <div className="space-y-3 py-2">
              <FormField label="User account">
                <Select value={form.user_id} onValueChange={(v) => setForm({ ...form, user_id: v })}>
                  <SelectTrigger className="rounded-xl h-9"><SelectValue placeholder="Select user" /></SelectTrigger>
                  <SelectContent>
                    {staffUsers.map((u) => (
                      <SelectItem key={u.id} value={u.id}>{u.name} ({ROLE_LABELS[u.role] || u.role})</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </FormField>
              <DepartmentSelect
                departments={departments}
                value={form.department}
                onChange={(v) => setForm({ ...form, department: v })}
                required={false}
                showSettingsHint
              />
              <FormField label="Specialization">
                <Input value={form.specialization} onChange={(e) => setForm({ ...form, specialization: e.target.value })} className="rounded-xl" />
              </FormField>
              <FormField label="Qualification">
                <Input value={form.qualification} onChange={(e) => setForm({ ...form, qualification: e.target.value })} className="rounded-xl" />
              </FormField>
              <FormField label="Consultation fee (₹)">
                <Input type="number" value={form.consultation_fee} onChange={(e) => setForm({ ...form, consultation_fee: e.target.value })} className="rounded-xl" />
              </FormField>
              <FormField label="Employment type">
                <Select value={form.employment_type} onValueChange={(v) => setForm({ ...form, employment_type: v })}>
                  <SelectTrigger className="rounded-xl h-9"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="permanent">Permanent</SelectItem>
                    <SelectItem value="visiting">Visiting</SelectItem>
                  </SelectContent>
                </Select>
              </FormField>
              <FormField label="Shift">
                <Input value={form.shift} onChange={(e) => setForm({ ...form, shift: e.target.value })} placeholder="Morning / Evening" className="rounded-xl" />
              </FormField>
              <FormField label="Ward assignment">
                <Input value={form.ward_assignment} onChange={(e) => setForm({ ...form, ward_assignment: e.target.value })} className="rounded-xl" />
              </FormField>
            </div>
            <DialogFooter>
              <Button onClick={createProfile} className="btn-primary">Create</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      ) : null}
    >
      <PageContent wide fill flush className="flex flex-col min-h-0 gap-1.5 pb-1">
        <GlassCard padding={false} className="shrink-0 px-2.5 sm:px-3 py-2">
          <div className="flex flex-wrap items-center gap-1.5 sm:gap-2">
            <ViewModeTabs modes={TAB_MODES} value={tab} onChange={setTab} />
            <div className="relative flex-1 min-w-[160px]">
              <MagnifyingGlass weight="regular" className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-text-muted pointer-events-none" />
              <Input
                data-testid="staff-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Search staff..."
                className="pl-8 rounded-lg h-8 text-ui-base glass-input border-white/60"
              />
            </div>
          </div>
        </GlassCard>

        <GlassCard padding={false} className="flex-1 min-h-0 overflow-hidden flex flex-col">
          <div className="flex-1 min-h-0 overflow-auto scrollbar-thin">
            <table className="w-full text-ui-sm table-dense min-w-[720px]">
              <thead className="sticky top-0 z-10 bg-white/75 backdrop-blur-sm border-b border-white/50">
                <tr>
                  <th className="text-left px-3 py-2 font-semibold text-text-secondary">Name</th>
                  <th className="text-left px-3 py-2 font-semibold text-text-secondary">Role</th>
                  <th className="text-left px-3 py-2 font-semibold text-text-secondary">Department</th>
                  <th className="text-left px-3 py-2 font-semibold text-text-secondary">Specialization</th>
                  <th className="text-left px-3 py-2 font-semibold text-text-secondary">Type</th>
                  <th className="text-left px-3 py-2 font-semibold text-text-secondary">Fee (₹)</th>
                  <th className="text-left px-3 py-2 font-semibold text-text-secondary">Shift</th>
                  <th className="text-left px-3 py-2 font-semibold text-text-secondary">Available</th>
                </tr>
              </thead>
              <tbody>
                {staff.length === 0 && (
                  <tr><td colSpan={8} className="px-3 py-8 text-center text-text-muted">No staff profiles yet</td></tr>
                )}
                {staff.map((s) => (
                  <tr
                    key={s.id}
                    className="border-b border-white/30 last:border-0 hover:bg-white/40 cursor-pointer transition-colors"
                    onClick={() => openDetail(s)}
                  >
                    <td className="px-3 py-2.5 font-medium text-[#022C22]">{s.name}</td>
                    <td className="px-3 py-2.5 capitalize">{ROLE_LABELS[s.role] || s.role}</td>
                    <td className="px-3 py-2.5">{s.department || "—"}</td>
                    <td className="px-3 py-2.5">{s.specialization || "—"}</td>
                    <td className="px-3 py-2.5 capitalize">{EMPLOYMENT_LABELS[s.employment_type] || s.employment_type || "—"}</td>
                    <td className="px-3 py-2.5">{s.role === "doctor" && s.consultation_fee != null ? s.consultation_fee : "—"}</td>
                    <td className="px-3 py-2.5">{s.shift || "—"}</td>
                    <td className="px-3 py-2.5">
                      <StatusPill
                        status={s.is_available ? "confirmed" : "cancelled"}
                        label={s.is_available ? "Yes" : "No"}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </GlassCard>

        <Sheet open={!!selected} onOpenChange={(v) => !v && setSelected(null)}>
          <SheetContent className="glass-card border-white/60 sm:max-w-md overflow-y-auto">
            {selected && (
              <>
                <SheetHeader className="text-left mb-4">
                  <div className="flex items-start gap-3">
                    <UserCircle weight="fill" className="w-10 h-10 text-[#064E3B] shrink-0" />
                    <div>
                      <SheetTitle className="font-heading text-[15px] text-[#022C22]">{selected.name}</SheetTitle>
                      <SheetDescription className="text-[12px] mt-0.5">
                        {ROLE_LABELS[selected.role]} · {selected.department || "No department"}
                        {selected.employment_type ? ` · ${EMPLOYMENT_LABELS[selected.employment_type] || selected.employment_type}` : ""}
                      </SheetDescription>
                    </div>
                  </div>
                </SheetHeader>

                {selected.qualification && (
                  <div className="text-[12px] text-[#022C22] mb-2">{selected.qualification}</div>
                )}
                {selected.ward_assignment && (
                  <div className="text-[12px] text-text-secondary mb-4">Ward: {selected.ward_assignment}</div>
                )}

                <div className="space-y-3 mb-4 pb-4 border-b border-white/40">
                  <DepartmentSelect
                    departments={departments}
                    value={selected.department || ""}
                    onChange={(v) => setSelected({ ...selected, department: v })}
                    required={false}
                    showSettingsHint
                  />
                  {selected.role === "doctor" && (
                    <FormField label="Consultation fee (₹)">
                      <Input
                        type="number"
                        value={selected.consultation_fee ?? ""}
                        onChange={(e) => setSelected({ ...selected, consultation_fee: e.target.value ? parseFloat(e.target.value) : null })}
                        className="rounded-xl h-9"
                      />
                    </FormField>
                  )}
                  <FormField label="Employment type">
                    <Select
                      value={selected.employment_type || "permanent"}
                      onValueChange={(v) => setSelected({ ...selected, employment_type: v })}
                    >
                      <SelectTrigger className="rounded-xl h-9"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="permanent">Permanent</SelectItem>
                        <SelectItem value="visiting">Visiting</SelectItem>
                      </SelectContent>
                    </Select>
                  </FormField>
                  <FormField label="Available for appointments / duty">
                    <Select
                      value={selected.is_available ? "yes" : "no"}
                      onValueChange={(v) => setSelected({ ...selected, is_available: v === "yes" })}
                    >
                      <SelectTrigger className="rounded-xl h-9"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="yes">Yes</SelectItem>
                        <SelectItem value="no">No</SelectItem>
                      </SelectContent>
                    </Select>
                  </FormField>
                  <Button onClick={updateProfile} className="btn-primary w-full">Save profile</Button>
                </div>

                {selected.role === "doctor" && (
                  <div className="mt-2 pt-4 border-t border-white/40">
                    <div className="flex items-center gap-2 mb-3">
                      <CalendarBlank weight="regular" className="w-4 h-4 text-[#064E3B]" />
                      <span className="font-heading text-[14px] font-semibold">Schedule</span>
                    </div>
                    {schedules.length === 0 ? (
                      <div className="text-[13px] text-text-muted mb-3">No schedule slots defined.</div>
                    ) : (
                      <div className="space-y-2 mb-4">
                        {schedules.map((sch) => (
                          <div key={sch.id} className="text-[12px] rounded-xl bg-white/50 border border-white/40 px-3 py-2">
                            {sch.specific_date
                              ? new Date(sch.specific_date).toLocaleDateString()
                              : DAYS[sch.day_of_week] ?? "—"}
                            {" · "}{sch.start_time?.slice(0, 5)} – {sch.end_time?.slice(0, 5)}
                            {sch.notes ? ` · ${sch.notes}` : ""}
                          </div>
                        ))}
                      </div>
                    )}
                    <div className="grid grid-cols-2 gap-2">
                      <Select value={scheduleForm.day_of_week} onValueChange={(v) => setScheduleForm({ ...scheduleForm, day_of_week: v })}>
                        <SelectTrigger className="rounded-xl h-9 text-[12px]"><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {DAYS.map((d, i) => <SelectItem key={d} value={String(i)}>{d}</SelectItem>)}
                        </SelectContent>
                      </Select>
                      <Input type="time" value={scheduleForm.start_time} onChange={(e) => setScheduleForm({ ...scheduleForm, start_time: e.target.value })} className="rounded-xl h-9" />
                      <Input type="time" value={scheduleForm.end_time} onChange={(e) => setScheduleForm({ ...scheduleForm, end_time: e.target.value })} className="rounded-xl h-9" />
                      <Button onClick={addSchedule} className="btn-primary text-[12px]">Add slot</Button>
                    </div>
                  </div>
                )}
              </>
            )}
          </SheetContent>
        </Sheet>
      </PageContent>
    </AppShell>
  );
};

export default Staff;

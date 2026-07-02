import React, { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { addDays, addWeeks, format, startOfWeek, subWeeks } from "date-fns";
import AppShell from "@/components/layout/AppShell";
import { api, normalizeApiError } from "@/lib/api";
import { unwrapPaged, buildPageQuery } from "@/lib/pagination";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from "@/components/ui/dialog";
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  Plus, CalendarBlank, CaretLeft, CaretRight, PencilSimple, CheckCircle, XCircle,
  MagnifyingGlass, Check, CalendarCheck, Prohibit,
} from "@phosphor-icons/react";
import { toast } from "sonner";
import PageContent from "@/components/glass/PageContent";
import KanbanBoard, { KanbanCard } from "@/components/glass/KanbanBoard";
import GlassCard from "@/components/glass/GlassCard";
import FormField from "@/components/forms/FormField";
import PatientSelect from "@/components/forms/PatientSelect";
import DoctorSelect from "@/components/forms/DoctorSelect";
import DepartmentSelect from "@/components/forms/DepartmentSelect";
import useDepartments from "@/hooks/useDepartments";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";
import { cn } from "@/lib/utils";

const APPOINTMENT_STATUS_LABELS = {
  scheduled: "Scheduled",
  confirmed: "Checked-in",
  completed: "Completed",
  cancelled: "Cancelled",
  no_show: "No-show",
};

const KANBAN_COLUMNS = [
  {
    id: "scheduled",
    label: "Scheduled",
    description: "Appointment is booked and waiting for the patient to arrive.",
    icon: CalendarBlank,
    iconColor: "text-blue-500",
    iconBg: "bg-blue-50",
    headerClass: "bg-white/50",
  },
  {
    id: "checked_in",
    label: "Checked in",
    description: "Patient has arrived or confirmed for the visit.",
    icon: CalendarCheck,
    iconColor: "text-teal-600",
    iconBg: "bg-teal-50",
    headerClass: "bg-white/50",
  },
  {
    id: "completed",
    label: "Completed",
    description: "Visit finished successfully.",
    icon: Check,
    iconColor: "text-emerald-600",
    iconBg: "bg-emerald-50",
    headerClass: "bg-white/50",
  },
  {
    id: "cancelled",
    label: "Cancelled",
    description: "Appointment was called off before the visit. Sends a cancellation message to the patient.",
    icon: Prohibit,
    iconColor: "text-slate-500",
    iconBg: "bg-slate-100",
    headerClass: "bg-white/50",
  },
  {
    id: "no_show",
    label: "No-show",
    description: "Patient did not arrive. Used for follow-ups and analytics; may be set automatically after a missed slot.",
    icon: XCircle,
    iconColor: "text-red-500",
    iconBg: "bg-red-50",
    headerClass: "bg-white/50",
  },
];

const statusToColumn = (status) => {
  if (status === "confirmed") return "checked_in";
  if (status === "scheduled") return "scheduled";
  if (status === "completed") return "completed";
  if (status === "cancelled") return "cancelled";
  if (status === "no_show") return "no_show";
  return null;
};

const columnToStatus = (columnId) => {
  if (columnId === "checked_in") return "confirmed";
  return columnId;
};

const emptyForm = () => ({
  patient_id: "", doctor_user_id: "", department: "", date: "", time: "10:00", notes: "",
});

const apptToEditForm = (a) => {
  const dt = new Date(a.scheduled_at);
  return {
    doctor_user_id: a.doctor_user_id || "",
    department: a.department || "",
    date: dt.toISOString().slice(0, 10),
    time: dt.toTimeString().slice(0, 5),
    notes: a.notes || "",
    status: a.status || "scheduled",
  };
};


const Appointments = () => {
  const [params] = useSearchParams();
  const { can } = usePermissions();
  const canCreateAppointment = can(PERMISSIONS.AppointmentCreate);
  const { departments } = useDepartments();
  const dateFilter = params.get("date");
  const [rows, setRows] = useState([]);
  const [patients, setPatients] = useState([]);
  const [bookingOptions, setBookingOptions] = useState({ doctors: [] });
  const [weekStart, setWeekStart] = useState(() => startOfWeek(new Date(), { weekStartsOn: 0 }));
  const [search, setSearch] = useState("");
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(emptyForm());
  const [editOpen, setEditOpen] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [editForm, setEditForm] = useState(null);
  const [cancelTarget, setCancelTarget] = useState(null);

  const load = () => {
    const qs = buildPageQuery({ page: 1, page_size: 500 });
    api.get(`/appointments?${qs}`).then((r) => {
      let items = unwrapPaged(r).items;
      if (dateFilter === "today") {
        const today = new Date();
        items = items.filter((a) => {
          const d = new Date(a.scheduled_at);
          return d.toDateString() === today.toDateString();
        });
      }
      setRows(items);
    });
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [dateFilter, weekStart]);

  useEffect(() => {
    const controller = new AbortController();
    api.get("/patients?page=1&page_size=300", { signal: controller.signal })
      .then((r) => setPatients(unwrapPaged(r).items))
      .catch(() => {});
    return () => controller.abort();
  }, []);

  useEffect(() => {
    api.get("/appointments/booking-options")
      .then((r) => setBookingOptions({ doctors: r.data?.doctors || [] }))
      .catch(() => setBookingOptions({ doctors: [] }));
  }, []);

  const weekEnd = addDays(weekStart, 6);

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((a) => {
      const patient = patients.find((p) => p.id === a.patient_id);
      return (
        a.patient_name?.toLowerCase().includes(q)
        || a.patient_id?.toLowerCase().includes(q)
        || patient?.phone?.includes(q)
        || a.doctor_name?.toLowerCase().includes(q)
      );
    });
  }, [rows, search, patients]);

  const visibleRows = useMemo(() => filteredRows.filter((a) => {
    const d = new Date(a.scheduled_at);
    return d >= weekStart && d <= addDays(weekStart, 7);
  }), [filteredRows, weekStart]);

  const create = async () => {
    if (!form.patient_id || !form.doctor_user_id || !form.department || !form.date) {
      toast.error("Please fill required fields"); return;
    }
    if (bookingOptions.doctors.length === 0) {
      toast.error("No doctors available. Add doctor profiles under Hospital Staff first.");
      return;
    }
    try {
      const dt = new Date(`${form.date}T${form.time}:00`);
      await api.post("/appointments", {
        patientId: form.patient_id,
        doctorUserId: form.doctor_user_id,
        department: form.department,
        scheduledAt: dt.toISOString(),
        notes: form.notes,
      });
      toast.success("Appointment scheduled");
      setOpen(false);
      setForm(emptyForm());
      load();
    } catch (e) { toast.error(normalizeApiError(e, "Failed to schedule appointment")); }
  };

  const openEdit = (a) => {
    setEditingId(a.id);
    setEditForm(apptToEditForm(a));
    setEditOpen(true);
  };

  const saveEdit = async () => {
    if (!editForm.doctor_user_id || !editForm.department || !editForm.date) {
      toast.error("Please fill required fields");
      return;
    }
    try {
      const dt = new Date(`${editForm.date}T${editForm.time}:00`);
      const previous = rows.find((r) => r.id === editingId);
      await api.patch(`/appointments/${editingId}`, {
        doctorUserId: editForm.doctor_user_id,
        department: editForm.department,
        scheduledAt: dt.toISOString(),
        notes: editForm.notes,
      });
      if (editForm.status && previous && editForm.status !== previous.status) {
        await api.patch(`/appointments/${editingId}/status`, { status: editForm.status });
      }
      toast.success("Appointment updated");
      setEditOpen(false);
      load();
    } catch (e) { toast.error(normalizeApiError(e, "Update failed")); }
  };

  const updateStatus = async (id, status) => {
    try {
      await api.patch(`/appointments/${id}/status`, { status });
      toast.success("Status updated");
      load();
    } catch { toast.error("Update failed"); }
  };

  const handleKanbanMove = (item, columnId) => {
    const nextStatus = columnToStatus(columnId);
    if (nextStatus && item.status !== nextStatus && item.status !== (columnId === "checked_in" ? "confirmed" : columnId)) {
      updateStatus(item.id, nextStatus);
    }
  };

  const renderApptCard = (a, { compact } = {}) => {
    const dept = a.department || "General Medicine";
    const doctorLabel = a.doctor_name ? ` · ${a.doctor_name}` : "";
    const scheduledAt = new Date(a.scheduled_at);
    const timeLabel = format(scheduledAt, compact ? "d MMM · h:mm a" : "MMM d, h:mm a");
    const iconSize = compact ? "w-3 h-3" : "w-3.5 h-3.5";
    const btnClass = "h-9 w-9 rounded-xl flex items-center justify-center hover:bg-white/60";
    const terminal = a.status === "cancelled" || a.status === "no_show" || a.status === "completed";

    return (
      <KanbanCard
        compact={compact}
        title={a.patient_name}
        subtitle={compact ? `${dept}${doctorLabel} · ${timeLabel}` : `${dept}${doctorLabel}`}
        meta={compact ? undefined : timeLabel}
        colorClass="bg-white/60 border-white/70"
        actions={(
          <>
            <button type="button" onClick={() => openEdit(a)} className={btnClass} title="Edit" aria-label="Edit appointment">
              <PencilSimple className={iconSize} />
            </button>
            {!terminal && (
              <button type="button" onClick={() => updateStatus(a.id, "completed")} className={btnClass} title="Mark completed" aria-label="Mark completed">
                <CheckCircle className={cn(iconSize, "text-emerald-600")} />
              </button>
            )}
            {a.status !== "cancelled" && a.status !== "no_show" && (
              <button type="button" onClick={() => setCancelTarget(a.id)} className={btnClass} title="Cancel appointment" aria-label="Cancel appointment">
                <XCircle className={cn(iconSize, "text-red-600")} />
              </button>
            )}
          </>
        )}
      />
    );
  };

  const newApptDialog = canCreateAppointment ? (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button data-testid="new-appt-btn" className="btn-primary rounded-xl h-9 px-4 shadow-sm">
          <Plus weight="bold" className="w-4 h-4 mr-1.5" /> New Appointment
        </Button>
      </DialogTrigger>
      <DialogContent className="rounded-xl max-w-lg glass-card border-white/60" data-testid="new-appt-dialog">
        <DialogHeader><DialogTitle className="font-heading">Schedule Appointment</DialogTitle></DialogHeader>
        <AppointmentFormFields
          form={form}
          setForm={setForm}
          patients={patients}
          bookingOptions={bookingOptions}
          departments={departments}
          showPatient
        />
        <DialogFooter>
          <Button variant="outline" onClick={() => setOpen(false)} className="rounded-xl h-9">Cancel</Button>
          <Button data-testid="appt-save-btn" onClick={create} className="btn-primary rounded-xl h-9">Schedule</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  ) : null;

  return (
    <AppShell
      title="Appointments"
      showDate={false}
      hideHeaderSearch
      hideHospitalBadge
      scrollable={false}
      compactFooter
      wide
      headerTrailing={(
        <label className="flex items-center gap-2.5 w-full sm:w-[280px] lg:w-[320px] h-9 px-3.5 rounded-full glass-input border-white/60 bg-white/45 cursor-text shrink-0">
          <MagnifyingGlass weight="regular" className="w-4 h-4 text-text-muted shrink-0" aria-hidden="true" />
          <input
            data-testid="appt-search"
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by patient name, ID or phone"
            className="flex-1 min-w-0 bg-transparent border-0 outline-none text-ui-sm text-[#022C22] placeholder:text-text-muted"
          />
        </label>
      )}
    >
      <PageContent wide fill flush className="grid grid-rows-[auto_minmax(0,1fr)] gap-2 sm:gap-3 pb-2 min-h-0 overflow-hidden">
        {/* Toolbar */}
        <div className="shrink-0 flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="outline"
              className="rounded-xl glass-input h-9 px-3 text-[13px] font-medium"
              onClick={() => setWeekStart(startOfWeek(new Date(), { weekStartsOn: 0 }))}
            >
              Today
            </Button>
            <Button variant="outline" className="rounded-xl glass-input h-9 w-9 p-0" onClick={() => setWeekStart(subWeeks(weekStart, 1))} aria-label="Previous week">
              <CaretLeft weight="bold" />
            </Button>
            <span className="text-ui-sm font-medium text-[#022C22] px-1 min-w-[140px] text-center">
              {format(weekStart, "MMM d")} – {format(weekEnd, "MMM d, yyyy")}
            </span>
            <Button variant="outline" className="rounded-xl glass-input h-9 w-9 p-0" onClick={() => setWeekStart(addWeeks(weekStart, 1))} aria-label="Next week">
              <CaretRight weight="bold" />
            </Button>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {newApptDialog}
          </div>
        </div>

        <GlassCard
          padding={false}
          className="min-h-0 flex-1 overflow-hidden p-1.5 sm:p-2 flex flex-col"
          data-testid="appointments-kanban"
        >
          <KanbanBoard
            fillHeight
            compact
            className="h-full min-h-0 flex-1"
            columns={KANBAN_COLUMNS}
            items={visibleRows}
            getColumnId={(a) => statusToColumn(a.status)}
            getItemKey={(a) => a.id}
            renderCard={renderApptCard}
            onMove={handleKanbanMove}
            emptyLabel="Drop here"
            maxVisible={50}
          />
        </GlassCard>
      </PageContent>

      <Dialog open={editOpen} onOpenChange={(v) => { setEditOpen(v); if (!v) { setEditingId(null); setEditForm(null); } }}>
        <DialogContent className="rounded-xl max-w-lg glass-card" data-testid="edit-appt-dialog">
          <DialogHeader>
            <DialogTitle className="font-heading flex items-center gap-2">
              <CalendarBlank weight="fill" className="w-4 h-4 text-[#064E3B]" /> Edit / Reschedule
            </DialogTitle>
          </DialogHeader>
          {editForm && (
            <>
              <AppointmentFormFields form={editForm} setForm={setEditForm} bookingOptions={bookingOptions} departments={departments} showStatus />
              <DialogFooter>
                <Button variant="outline" onClick={() => setEditOpen(false)} className="rounded-xl h-9">Cancel</Button>
                <Button onClick={saveEdit} className="btn-primary rounded-xl h-9">Save changes</Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>

      <AlertDialog open={!!cancelTarget} onOpenChange={(v) => !v && setCancelTarget(null)}>
        <AlertDialogContent className="rounded-xl glass-card">
          <AlertDialogHeader>
            <AlertDialogTitle className="font-heading">Cancel appointment?</AlertDialogTitle>
            <AlertDialogDescription>This will mark the appointment as cancelled.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel className="rounded-xl h-9">Keep appointment</AlertDialogCancel>
            <AlertDialogAction onClick={() => { updateStatus(cancelTarget, "cancelled"); setCancelTarget(null); }} className="rounded-xl h-9 bg-red-600 hover:bg-red-700">
              Yes, cancel
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </AppShell>
  );
};

const AppointmentFormFields = ({ form, setForm, patients, bookingOptions, departments = [], showPatient, showStatus }) => {
  const doctors = bookingOptions?.doctors || [];

  const handleDoctorChange = (userId) => {
    const doc = doctors.find((d) => d.user_id === userId);
    setForm({
      ...form,
      doctor_user_id: userId,
      department: doc?.department || form.department,
    });
  };

  return (
    <div className="space-y-3">
      {showPatient && (
        <PatientSelect
          patients={patients}
          value={form.patient_id}
          onChange={(v) => setForm({ ...form, patient_id: v })}
          testId="appt-patient"
        />
      )}
      {doctors.length === 0 && (
        <p className="text-[12px] text-amber-700 bg-amber-50/80 border border-amber-200/60 rounded-xl px-3 py-2">
          No doctors available. Add a doctor under Hospital Staff.
        </p>
      )}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <DoctorSelect
          doctors={doctors}
          value={form.doctor_user_id}
          onChange={handleDoctorChange}
        />
        <DepartmentSelect
          departments={departments}
          value={form.department}
          onChange={(v) => setForm({ ...form, department: v })}
          showSettingsHint
        />
      </div>
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
      <div>
        <FormField label="Date" required>
          <Input type="date" value={form.date} onChange={(e) => setForm({ ...form, date: e.target.value })} className="rounded-xl" />
        </FormField>
      </div>
      <div>
        <FormField label="Time">
          <Input type="time" value={form.time} onChange={(e) => setForm({ ...form, time: e.target.value })} className="rounded-xl" />
        </FormField>
      </div>
    </div>
    {showStatus && (
      <FormField label="Status">
        <Select value={form.status} onValueChange={(v) => setForm({ ...form, status: v })}>
          <SelectTrigger className="rounded-xl h-9"><SelectValue /></SelectTrigger>
          <SelectContent>
            {["scheduled", "confirmed", "completed", "cancelled", "no_show"].map((s) => (
              <SelectItem key={s} value={s}>{APPOINTMENT_STATUS_LABELS[s] || s}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </FormField>
    )}
    <FormField label="Notes">
      <Textarea value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} className="rounded-xl" rows={2} />
    </FormField>
  </div>
  );
};

export default Appointments;

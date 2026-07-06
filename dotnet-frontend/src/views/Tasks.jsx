import React, { useEffect, useMemo, useState } from "react";
import { format, isPast, isToday, isThisWeek } from "date-fns";
import AppShell from "@/components/layout/AppShell";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from "@/components/ui/dialog";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  Plus, Trash, WhatsappLogo, MagnifyingGlass,
  CalendarBlank, Clock, Warning, CheckCircle,
} from "@phosphor-icons/react";
import { toast } from "sonner";
import PageContent from "@/components/glass/PageContent";
import KanbanBoard, { KanbanCard } from "@/components/glass/KanbanBoard";
import GlassCard from "@/components/glass/GlassCard";
import StatusPill from "@/components/glass/StatusPill";
import EmptyState from "@/components/ui/EmptyState";

const KANBAN_COLUMNS = [
  {
    id: "due_today",
    label: "Due Today",
    icon: CalendarBlank,
    iconColor: "text-sky-600",
    iconBg: "bg-sky-50",
    headerClass: "bg-sky-50/80",
  },
  {
    id: "this_week",
    label: "This Week",
    icon: Clock,
    iconColor: "text-amber-700",
    iconBg: "bg-amber-50",
    headerClass: "bg-amber-50/80",
  },
  {
    id: "overdue",
    label: "Overdue",
    icon: Warning,
    iconColor: "text-red-600",
    iconBg: "bg-red-50",
    headerClass: "bg-red-50/80",
  },
  {
    id: "completed",
    label: "Completed",
    icon: CheckCircle,
    iconColor: "text-emerald-600",
    iconBg: "bg-emerald-50",
    headerClass: "bg-emerald-50/80",
    testId: "followups-col-completed",
  },
];

const normalizeTaskStatus = (status) => {
  if (status == null) return "pending";
  const s = String(status).toLowerCase();
  if (s === "done" || s === "completed") return "done";
  if (s === "in_progress" || s === "inprogress") return "in_progress";
  if (s === "cancelled" || s === "canceled") return "cancelled";
  return s;
};

const isTaskDone = (status) => normalizeTaskStatus(status) === "done";

const getTaskColumn = (task) => {
  if (isTaskDone(task.status)) return "completed";
  if (!task.due_at) return "this_week";
  const due = new Date(task.due_at);
  if (isPast(due) && !isToday(due) && !isTaskDone(task.status)) return "overdue";
  if (isToday(due)) return "due_today";
  if (isThisWeek(due, { weekStartsOn: 0 })) return "this_week";
  return "this_week";
};

const emptyForm = () => ({
  title: "", type: "follow_up", priority: "medium", due_at: "", notes: "",
});

const Tasks = () => {
  const [rows, setRows] = useState([]);
  const [search, setSearch] = useState("");
  const [open, setOpen] = useState(false);
  const [loadError, setLoadError] = useState(null);
  const [form, setForm] = useState(emptyForm());

  const load = () => {
    setLoadError(null);
    api.get("/tasks")
      .then((r) => {
        const items = (r.data || []).map((t) => ({ ...t, status: normalizeTaskStatus(t.status) }));
        setRows(items);
      })
      .catch((err) => {
        setRows([]);
        setLoadError(err.response?.status === 403
          ? "You don't have permission to view follow-up tasks."
          : "Failed to load tasks.");
      });
  };

  useEffect(() => { load(); }, []);

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((t) =>
      t.title?.toLowerCase().includes(q)
      || t.patient_name?.toLowerCase().includes(q)
      || t.type?.toLowerCase().includes(q)
      || t.notes?.toLowerCase().includes(q),
    );
  }, [rows, search]);

  const create = async () => {
    if (!form.title) { toast.error("Title required"); return; }
    try {
      await api.post("/tasks", {
        title: form.title,
        type: form.type,
        priority: form.priority,
        due_at: form.due_at ? new Date(form.due_at).toISOString() : null,
        notes: form.notes,
      });
      toast.success("Task created");
      setOpen(false);
      setForm(emptyForm());
      load();
    } catch {
      toast.error("Failed");
    }
  };

  const updateStatus = async (id, status) => {
    const normalized = normalizeTaskStatus(status);
    setRows((prev) => prev.map((t) => (t.id === id ? { ...t, status: normalized } : t)));
    try {
      await api.patch(`/tasks/${id}`, { status: normalized });
      toast.success(normalized === "done" ? "Marked complete" : "Updated");
      load();
    } catch {
      toast.error("Failed to update task");
      load();
    }
  };

  const remove = async (id) => {
    await api.delete(`/tasks/${id}`);
    toast.success("Removed");
    load();
  };

  const handleKanbanMove = (item, columnId) => {
    if (columnId === "completed") updateStatus(item.id, "done");
  };

  const renderTaskCard = (t) => (
    <KanbanCard
      compact
      colorClass="bg-white/70 border-white/80"
      title={t.title}
      subtitle={[t.type?.replace(/_/g, " "), t.patient_name].filter(Boolean).join(" · ")}
      meta={t.due_at ? format(new Date(t.due_at), "EEE, MMM d · h:mm a") : "No due date"}
      badge={<StatusPill status={t.priority} />}
      data-testid={`followup-card-${t.id}`}
      actions={(
        <>
          <button
            type="button"
            data-testid={`followup-complete-${t.id}`}
            onClick={() => updateStatus(t.id, isTaskDone(t.status) ? "pending" : "done")}
            className="text-ui-caption px-2 py-0.5 rounded-lg bg-emerald-50 text-emerald-700"
          >
            {isTaskDone(t.status) ? "Reopen" : "Complete"}
          </button>
          <button type="button" className="p-1 rounded-lg hover:bg-white/60" title="WhatsApp" aria-label="Open WhatsApp">
            <WhatsappLogo className="w-3.5 h-3.5 text-[#25D366]" weight="fill" />
          </button>
          <button type="button" onClick={() => remove(t.id)} className="p-1 rounded-lg hover:bg-white/60 text-red-600" aria-label="Delete follow-up">
            <Trash className="w-3.5 h-3.5" />
          </button>
        </>
      )}
    />
  );

  const newTaskDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button data-testid="new-task-btn" className="btn-primary h-9 rounded-xl text-ui-base">
          <Plus weight="bold" className="w-3.5 h-3.5 mr-1.5" />
          New Follow-up
        </Button>
      </DialogTrigger>
      <DialogContent className="rounded-2xl glass-card max-w-lg">
        <DialogHeader><DialogTitle className="font-heading">New Follow-up Task</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div>
            <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Title *</label>
            <Input data-testid="task-title" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} className="rounded-xl" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Type</label>
              <Select value={form.type} onValueChange={(v) => setForm({ ...form, type: v })}>
                <SelectTrigger className="rounded-xl h-9"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {["follow_up", "callback", "appointment_reminder", "post_visit_checkin", "re_engagement", "custom"].map((v) => (
                    <SelectItem key={v} value={v}>{v.replace(/_/g, " ")}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Priority</label>
              <Select value={form.priority} onValueChange={(v) => setForm({ ...form, priority: v })}>
                <SelectTrigger className="rounded-xl h-9"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {["low", "medium", "high", "emergency"].map((v) => <SelectItem key={v} value={v}>{v}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div>
            <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Due</label>
            <Input type="datetime-local" value={form.due_at} onChange={(e) => setForm({ ...form, due_at: e.target.value })} className="rounded-xl" />
          </div>
          <div>
            <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Notes</label>
            <Textarea value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} className="rounded-xl" rows={2} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => setOpen(false)} className="rounded-xl">Cancel</Button>
          <Button data-testid="task-save-btn" onClick={create} className="btn-primary">Create</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  return (
    <AppShell
      title="Follow-ups"
      subtitle="Track and complete patient follow-up tasks"
      hideHeaderSearch
      hideHospitalBadge
      showDate={false}
      scrollable={false}
      compactFooter
      wide
      headerTrailing={(
        <label className="flex items-center gap-2.5 w-full sm:w-[240px] lg:w-[280px] h-9 px-3.5 rounded-full glass-input border-white/60 bg-white/45 cursor-text shrink-0">
          <MagnifyingGlass weight="regular" className="w-4 h-4 text-text-muted shrink-0" aria-hidden="true" />
          <input
            data-testid="task-search"
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search follow-ups..."
            className="flex-1 min-w-0 bg-transparent border-0 outline-none text-ui-sm text-[#022C22] placeholder:text-text-muted"
          />
        </label>
      )}
      actions={newTaskDialog}
    >
      <PageContent wide fill flush className="flex flex-col min-h-0 overflow-hidden pb-2 p-2 sm:p-3">
        {loadError && (
          <GlassCard className="text-amber-800 bg-amber-50/80 border-amber-200/50 text-ui-sm shrink-0">{loadError}</GlassCard>
        )}

        <GlassCard
          padding={false}
          className="flex-1 min-h-0 overflow-hidden p-2 sm:p-3 flex flex-col"
          data-testid="followups-kanban"
        >
          {!loadError && filteredRows.length === 0 ? (
            <EmptyState
              testId="tasks-empty-state"
              illustration="/design/empty-state-tasks.svg"
              title="No follow-ups yet"
              description={search.trim()
                ? "No follow-ups match your search. Try a different keyword."
                : "Create follow-up tasks to track callbacks, reminders, and patient re-engagement."}
              action={(
                <Button
                  data-testid="empty-new-task-btn"
                  onClick={() => setOpen(true)}
                  className="btn-primary rounded-xl h-10 min-h-[44px] px-4"
                >
                  <Plus weight="bold" className="w-4 h-4 mr-1.5" />
                  New follow-up
                </Button>
              )}
            />
          ) : (
          <KanbanBoard
            fillHeight
            className="h-full min-h-0 flex-1"
            columns={KANBAN_COLUMNS}
            items={filteredRows}
            getColumnId={getTaskColumn}
            getItemKey={(t) => t.id}
            renderCard={renderTaskCard}
            onMove={handleKanbanMove}
            emptyLabels={{
              due_today: "No follow-ups due today",
              this_week: "No follow-ups scheduled this week",
              overdue: "No overdue follow-ups",
              completed: "Drop here to mark completed",
            }}
            maxVisible={50}
          />
          )}
        </GlassCard>
      </PageContent>
    </AppShell>
  );
};

export default Tasks;

import React, { useEffect, useMemo, useState } from "react";
import AppShell from "@/components/layout/AppShell";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from "@/components/ui/dialog";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  Plus, MagnifyingGlass, Funnel, SlidersHorizontal, UploadSimple,
  UserCircle, Phone, Handshake, Star,
} from "@phosphor-icons/react";
import { toast } from "sonner";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import ReferralKanban from "@/components/referrals/ReferralKanban";
import ReferralSidebar from "@/components/referrals/ReferralSidebar";

const CATEGORY_LABELS = {
  family_gp: "Family GP",
  specialist: "Specialist",
  consultant: "Consultant",
  clinic: "Clinic",
  hospital: "Hospital",
  other: "Other",
};

const KANBAN_COLUMNS = [
  {
    id: "lead",
    label: "Lead",
    subtitle: "New leads, not yet contacted",
    icon: UserCircle,
    iconColor: "text-blue-600",
    iconBg: "bg-blue-50",
    headerClass: "bg-blue-50/70",
    accentClass: "border-t-[3px] border-t-blue-400",
  },
  {
    id: "contacted",
    label: "Contacted",
    subtitle: "Contacted, waiting for response",
    icon: Phone,
    iconColor: "text-teal-600",
    iconBg: "bg-teal-50",
    headerClass: "bg-teal-50/70",
    accentClass: "border-t-[3px] border-t-teal-400",
  },
  {
    id: "referred",
    label: "Referred",
    subtitle: "Actively referring patients",
    icon: Handshake,
    iconColor: "text-violet-600",
    iconBg: "bg-violet-50",
    headerClass: "bg-violet-50/70",
    accentClass: "border-t-[3px] border-t-violet-400",
  },
  {
    id: "converted",
    label: "Converted",
    subtitle: "Converted to paying patients",
    icon: Star,
    iconColor: "text-amber-600",
    iconBg: "bg-amber-50",
    headerClass: "bg-amber-50/70",
    accentClass: "border-t-[3px] border-t-amber-400",
  },
];

const getDoctorColumn = (d) => {
  if (!d.last_contact_at) return "lead";
  if ((d.patients_referred || 0) === 0) return "contacted";
  if (d.needs_reconnect) return "referred";
  return "converted";
};

const emptyForm = () => ({
  name: "", clinic: "", specialty: "", phone: "", email: "",
  category: "specialist", tags: "", notes: "", reconnect_every_days: 30,
});

const Doctors = () => {
  const [docs, setDocs] = useState([]);
  const [analytics, setAnalytics] = useState({ total_referrals: 0, total_revenue: 0, top_doctors: [] });
  const [q, setQ] = useState("");
  const [cat, setCat] = useState("all");
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(emptyForm());

  const load = () => {
    const qs = new URLSearchParams();
    if (q) qs.append("q", q);
    if (cat !== "all") qs.append("category", cat);
    api.get(`/doctors?${qs}`).then((r) => setDocs(r.data || [])).catch(() => setDocs([]));
    api.get("/referrals/analytics").then((r) => setAnalytics(r.data || {})).catch(() => {});
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [q, cat]);

  const filteredDocs = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return docs;
    return docs.filter((d) =>
      d.name?.toLowerCase().includes(query)
      || d.clinic?.toLowerCase().includes(query)
      || d.specialty?.toLowerCase().includes(query),
    );
  }, [docs, q]);

  const create = async () => {
    if (!form.name) { toast.error("Name is required"); return; }
    try {
      await api.post("/doctors", {
        name: form.name,
        clinic: form.clinic || null,
        specialty: form.specialty || null,
        phone: form.phone || null,
        category: form.category,
        reconnect_days: parseInt(form.reconnect_every_days, 10) || 30,
      });
      toast.success("Referrer added");
      setOpen(false);
      setForm(emptyForm());
      load();
    } catch (e) {
      toast.error(e.response?.data?.detail || "Failed");
    }
  };

  const markContacted = async (id) => {
    await api.post(`/doctors/${id}/mark-contacted`);
    toast.success("Marked as contacted");
    load();
  };

  const handleKanbanMove = (item, columnId) => {
    if (columnId === "contacted" || columnId === "converted" || columnId === "referred") {
      if (!item.last_contact_at) markContacted(item.id);
    }
  };

  const addReferrerDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button data-testid="new-doctor-btn" className="btn-primary h-9 rounded-xl text-ui-base">
          <Plus weight="bold" className="w-3.5 h-3.5 mr-1.5" />
          Add Referrer
        </Button>
      </DialogTrigger>
      <DialogContent className="rounded-2xl glass-card max-w-lg" data-testid="new-doctor-dialog">
        <DialogHeader><DialogTitle className="font-heading">Add Referring Doctor</DialogTitle></DialogHeader>
        <div className="grid grid-cols-2 gap-3">
          <div className="col-span-2">
            <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Name *</label>
            <Input data-testid="nd-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="rounded-xl" placeholder="Dr. ..." />
          </div>
          <div>
            <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Hospital / Clinic</label>
            <Input value={form.clinic} onChange={(e) => setForm({ ...form, clinic: e.target.value })} className="rounded-xl" />
          </div>
          <div>
            <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Specialty</label>
            <Input value={form.specialty} onChange={(e) => setForm({ ...form, specialty: e.target.value })} className="rounded-xl" />
          </div>
          <div>
            <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Phone</label>
            <Input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} className="rounded-xl" />
          </div>
          <div>
            <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Category</label>
            <Select value={form.category} onValueChange={(v) => setForm({ ...form, category: v })}>
              <SelectTrigger className="rounded-xl h-9"><SelectValue /></SelectTrigger>
              <SelectContent>
                {Object.entries(CATEGORY_LABELS).map(([k, v]) => <SelectItem key={k} value={k}>{v}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="col-span-2">
            <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Reconnect every (days)</label>
            <Input type="number" value={form.reconnect_every_days} onChange={(e) => setForm({ ...form, reconnect_every_days: e.target.value })} className="rounded-xl max-w-[160px]" />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => setOpen(false)} className="rounded-xl">Cancel</Button>
          <Button data-testid="nd-save-btn" onClick={create} className="btn-primary">Add</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  return (
    <AppShell
      title="Referral CRM"
      subtitle="Manage and track your referral relationships"
      hideHeaderSearch
      hideHospitalBadge
      showDate={false}
      scrollable={false}
      compactFooter
      wide
      headerTrailing={(
        <label className="flex items-center gap-2.5 w-full sm:w-[260px] lg:w-[320px] h-9 px-3.5 rounded-full glass-input border-white/60 bg-white/45 cursor-text shrink-0">
          <MagnifyingGlass weight="regular" className="w-4 h-4 text-slate-400 shrink-0" aria-hidden="true" />
          <input
            data-testid="doctors-search"
            type="search"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Search doctors, hospitals, specialties..."
            className="flex-1 min-w-0 bg-transparent border-0 outline-none text-ui-sm text-[#022C22] placeholder:text-text-muted"
          />
        </label>
      )}
      actions={addReferrerDialog}
    >
      <div className="flex h-full min-h-0 gap-4 md:gap-5 overflow-hidden">
        <PageContent wide fill flush className="flex-1 min-w-0 grid grid-rows-[auto_minmax(0,1fr)] gap-2 min-h-0 overflow-hidden pb-2 pl-2 sm:pl-3 pr-0 pt-2 sm:pt-3">
        {/* Pipeline toolbar */}
        <div className="shrink-0 flex flex-wrap items-center justify-between gap-2 pr-2 sm:pr-3">
          <div className="flex items-center gap-2 min-w-0">
            <Funnel weight="duotone" className="w-4 h-4 text-[#6366F1] shrink-0" />
            <span className="font-heading text-ui-base font-semibold text-[#022C22]">Pipeline Funnel</span>
            <span className="text-ui-caption text-text-muted tabular-nums">({filteredDocs.length})</span>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <Dialog open={filtersOpen} onOpenChange={setFiltersOpen}>
              <DialogTrigger asChild>
                <Button variant="outline" size="sm" className="rounded-xl glass-input h-8 text-ui-sm" data-testid="referral-filters-btn">
                  <SlidersHorizontal weight="regular" className="w-3.5 h-3.5 mr-1.5" />
                  Filters
                </Button>
              </DialogTrigger>
              <DialogContent className="rounded-2xl glass-card max-w-sm">
                <DialogHeader><DialogTitle className="font-heading">Filters</DialogTitle></DialogHeader>
                <div>
                  <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1.5">Category</label>
                  <Select value={cat} onValueChange={setCat}>
                    <SelectTrigger className="rounded-xl h-9 glass-input" data-testid="doctor-cat-filter">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All Categories</SelectItem>
                      {Object.entries(CATEGORY_LABELS).map(([k, v]) => <SelectItem key={k} value={k}>{v}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <DialogFooter>
                  <Button variant="outline" onClick={() => setFiltersOpen(false)} className="rounded-xl">Close</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
            <Button
              variant="outline"
              size="sm"
              className="rounded-xl glass-input h-8 text-ui-sm"
              onClick={() => toast.info("Import coming soon")}
            >
              <UploadSimple weight="regular" className="w-3.5 h-3.5 mr-1.5" />
              Import
            </Button>
          </div>
        </div>

        {/* Kanban pipeline */}
        <div className="min-h-0 h-full overflow-hidden pr-2 sm:pr-3" data-testid="referral-kanban">
          {filteredDocs.length === 0 ? (
            <GlassCard className="h-full flex flex-col items-center justify-center text-center py-12">
              <UserCircle weight="duotone" className="w-10 h-10 text-text-muted/50 mb-2" />
              <div className="font-heading text-ui-base text-[#022C22] mb-1">No referring doctors yet</div>
              <div className="text-ui-sm text-text-secondary">Add a referrer to start building your pipeline.</div>
            </GlassCard>
          ) : (
            <ReferralKanban
              fillHeight
              className="h-full min-h-0"
              columns={KANBAN_COLUMNS}
              items={filteredDocs}
              getColumnId={getDoctorColumn}
              getItemKey={(d) => d.id}
              onMove={handleKanbanMove}
              emptyLabel="+ Drop here"
            />
          )}
        </div>
        </PageContent>

        <ReferralSidebar analytics={analytics} doctors={docs} />
      </div>
    </AppShell>
  );
};

export default Doctors;

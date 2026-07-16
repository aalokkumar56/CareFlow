import React, { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "@/lib/navigation";
import AppShell from "@/components/layout/AppShell";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import KanbanBoard from "@/components/glass/KanbanBoard";
import CampaignCard, { SuggestedDraftCard } from "@/components/campaigns/CampaignCard";
import { api, normalizeApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from "@/components/ui/dialog";
import {
  Plus, Sparkle, Clock, Users, CaretLeft, CaretRight, Megaphone, FileText, PaperPlaneTilt,
} from "@phosphor-icons/react";
import { toast } from "sonner";
import TemplatePlaceholdersPanel from "@/components/TemplatePlaceholdersPanel";
import useDepartments from "@/hooks/useDepartments";
import { cn } from "@/lib/utils";

const STATUSES = ["new_inquiry", "contacted", "appointment_scheduled", "visited", "re_engagement"];

const KANBAN_COLUMNS = [
  {
    id: "draft",
    label: "Draft",
    icon: FileText,
    headerClass: "bg-slate-100/80",
    iconBg: "bg-slate-200/80",
    iconColor: "text-slate-600",
  },
  {
    id: "scheduled",
    label: "Scheduled",
    icon: Clock,
    headerClass: "bg-amber-100/80",
    iconBg: "bg-amber-200/80",
    iconColor: "text-amber-700",
  },
  {
    id: "sent",
    label: "Sent",
    icon: PaperPlaneTilt,
    headerClass: "bg-emerald-100/80",
    iconBg: "bg-emerald-200/80",
    iconColor: "text-emerald-700",
  },
];

const statusToColumn = (c) => {
  const s = (c.status || "").toLowerCase();
  if (s === "sent" || s === "sending") return "sent";
  if (s === "scheduled") return "scheduled";
  return "draft";
};

const formatScheduled = (iso) => {
  if (!iso) return null;
  return new Date(iso).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
};

const WhatsAppPreview = ({ body, className }) => (
  <div className={className}>
    <div className="rounded-xl bg-[#DCF8C6]/90 border border-[#16A34A]/25 px-3 py-2.5 text-ui-sm text-[#022C22] whitespace-pre-wrap line-clamp-3 shadow-sm relative">
      <span className="absolute -left-1 top-3 w-2 h-2 bg-[#DCF8C6] border-l border-b border-[#16A34A]/20 rotate-45" aria-hidden />
      {body || "Your message preview…"}
    </div>
  </div>
);

const Campaigns = () => {
  const navigate = useNavigate();
  const { departments } = useDepartments();
  const [rows, setRows] = useState([]);
  const [drafts, setDrafts] = useState([]);
  const [search, setSearch] = useState("");
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({
    name: "", description: "", message_body: "",
    tags: "", departments: [], statuses: [], inactive_days: "",
    auto_send: false, scheduled_at: "",
  });
  const [preview, setPreview] = useState(null);
  const [previewing, setPreviewing] = useState(false);
  const [tags, setTags] = useState([]);
  const msgRef = useRef(null);
  const carouselRef = useRef(null);

  const load = () => {
    api.get("/campaigns").then((r) => setRows(r.data || [])).catch(() => setRows([]));
    api.get("/campaigns/suggested-drafts").then((r) => setDrafts(r.data || [])).catch(() => setDrafts([]));
  };

  useEffect(() => {
    load();
    api.get("/tags").then((r) => setTags(r.data || [])).catch(() => setTags([]));
  }, []);

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((c) =>
      c.name?.toLowerCase().includes(q)
      || c.description?.toLowerCase().includes(q)
      || c.message_body?.toLowerCase().includes(q),
    );
  }, [rows, search]);

  const resetForm = () => {
    setForm({
      name: "", description: "", message_body: "",
      tags: "", departments: [], statuses: [], inactive_days: "",
      auto_send: false, scheduled_at: "",
    });
    setPreview(null);
  };

  const openFromDraft = (draft) => {
    setForm({
      name: draft.draft_name || draft.name,
      description: `${draft.category?.replace(/_/g, " ")} · ${draft.event_date}`,
      message_body: draft.suggested_message || "",
      tags: "", departments: [], statuses: [], inactive_days: "",
      auto_send: false,
      scheduled_at: draft.event_date ? `${draft.event_date}T09:00` : "",
    });
    setPreview(null);
    setOpen(true);
  };

  const insertPlaceholder = (token) => {
    const el = msgRef.current;
    if (el && typeof el.selectionStart === "number") {
      const start = el.selectionStart;
      const end = el.selectionEnd;
      const body = form.message_body;
      const next = body.slice(0, start) + token + body.slice(end);
      setForm({ ...form, message_body: next });
      requestAnimationFrame(() => {
        el.focus();
        const pos = start + token.length;
        el.setSelectionRange(pos, pos);
      });
      return;
    }
    setForm({ ...form, message_body: `${form.message_body}${token}` });
  };

  const buildAudience = () => ({
    tags: form.tags ? form.tags.split(",").map((t) => t.trim()).filter(Boolean) : [],
    departments: form.departments,
    statuses: form.statuses,
    inactive_days: form.inactive_days ? parseInt(form.inactive_days, 10) : null,
  });

  const previewAudience = async () => {
    setPreviewing(true);
    try {
      const r = await api.post("/campaigns/preview-audience", buildAudience());
      setPreview(r.data);
    } catch {
      toast.error("Preview failed");
    } finally {
      setPreviewing(false);
    }
  };

  const create = async () => {
    if (!form.name || !form.message_body) {
      toast.error("Name and message body required");
      return;
    }
    if (form.auto_send && !form.scheduled_at) {
      toast.error("Pick a date and time for auto-send");
      return;
    }
    try {
      const payload = {
        name: form.name,
        description: form.description,
        messageBody: form.message_body,
        audience: buildAudience(),
      };
      if (form.auto_send && form.scheduled_at) {
        payload.scheduledAt = new Date(form.scheduled_at).toISOString();
      }
      const r = await api.post("/campaigns", payload);
      toast.success(
        r.data.status === "scheduled"
          ? `Campaign scheduled for ${formatScheduled(form.scheduled_at)}`
          : "Campaign created as draft",
      );
      setOpen(false);
      resetForm();
      load();
    } catch (e) {
      toast.error(e.response?.data?.detail || "Failed");
    }
  };

  const toggle = (key, value) => {
    const arr = form[key];
    if (arr.includes(value)) setForm({ ...form, [key]: arr.filter((x) => x !== value) });
    else setForm({ ...form, [key]: [...arr, value] });
  };

  const handleKanbanMove = async (campaign, columnId) => {
    const current = statusToColumn(campaign);
    if (current === columnId) return;

    if (columnId === "sent") {
      toast.error("Campaigns move to Sent automatically after publishing");
      return;
    }
    if (current === "sent" || campaign.status === "sending") {
      toast.error("Sent campaigns cannot be moved");
      return;
    }

    try {
      if (columnId === "draft") {
        await api.patch(`/campaigns/${campaign.id}`, { status: "draft", clear_schedule: true });
      } else if (columnId === "scheduled") {
        const scheduledAt = campaign.scheduled_at || new Date(Date.now() + 86400000).toISOString();
        await api.patch(`/campaigns/${campaign.id}`, { status: "scheduled", scheduled_at: scheduledAt });
      }
      toast.success("Campaign moved");
      load();
    } catch (e) {
      toast.error(normalizeApiError(e, "Could not move campaign"));
    }
  };

  const scrollCarousel = (dir) => {
    carouselRef.current?.scrollBy({ left: dir * 228, behavior: "smooth" });
  };

  return (
    <AppShell
      title="Campaigns"
      subtitle="Create, manage and track your marketing campaigns"
      hideHeaderSearch
      hideHospitalBadge
      showDate={false}
      scrollable={false}
      compactFooter
      wide
      actions={(
        <Dialog open={open} onOpenChange={(v) => { setOpen(v); if (!v) resetForm(); }}>
          <DialogTrigger asChild>
            <Button data-testid="new-campaign-btn" className="btn-primary h-9 rounded-xl text-ui-base">
              <Plus weight="bold" className="w-3.5 h-3.5 mr-1.5" />
              Create Campaign
            </Button>
          </DialogTrigger>
          <DialogContent className="rounded-xl max-w-2xl max-h-[85vh] overflow-y-auto glass-card" data-testid="new-campaign-dialog">
            <DialogHeader><DialogTitle className="font-heading">Create Campaign</DialogTitle></DialogHeader>
            <div className="space-y-4">
              <div>
                <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Campaign Name *</label>
                <Input data-testid="nc-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="rounded-xl" placeholder="Diwali Health Checkup" />
              </div>
              <div>
                <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Description</label>
                <Input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} className="rounded-xl" />
              </div>
              <div>
                <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">WhatsApp Message *</label>
                <TemplatePlaceholdersPanel onInsert={insertPlaceholder} className="mb-2" />
                <Textarea
                  ref={msgRef}
                  data-testid="nc-message"
                  value={form.message_body}
                  onChange={(e) => setForm({ ...form, message_body: e.target.value })}
                  className="rounded-xl"
                  rows={4}
                  placeholder="Hi {name}, our diabetes care package is..."
                />
                {form.message_body && <WhatsAppPreview body={form.message_body} className="mt-2" />}
              </div>

              <GlassCard className="bg-white/50 p-4">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <div className="text-ui-label uppercase tracking-[0.08em] font-semibold text-[#022C22] flex items-center gap-1.5">
                      <Clock weight="duotone" className="w-4 h-4 text-blue-700" />
                      Auto-send on schedule
                    </div>
                    <p className="text-ui-label text-text-secondary mt-0.5">
                      When enabled, the campaign sends automatically at the chosen date &amp; time.
                    </p>
                  </div>
                  <Switch
                    data-testid="nc-auto-send"
                    checked={form.auto_send}
                    onCheckedChange={(c) => setForm({ ...form, auto_send: c })}
                  />
                </div>
                {form.auto_send && (
                  <div className="mt-3">
                    <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">Send at (local time)</label>
                    <Input
                      type="datetime-local"
                      data-testid="nc-scheduled-at"
                      value={form.scheduled_at}
                      onChange={(e) => setForm({ ...form, scheduled_at: e.target.value })}
                      className="rounded-xl max-w-xs"
                    />
                  </div>
                )}
              </GlassCard>

              <div className="border-t border-white/50 pt-4">
                <div className="text-ui-caption uppercase tracking-[0.1em] font-semibold text-text-secondary mb-3">Audience Filters</div>
                <div className="space-y-3">
                  <div>
                    <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1.5">Tags (comma)</label>
                    <Input data-testid="nc-tags" value={form.tags} onChange={(e) => setForm({ ...form, tags: e.target.value })} className="rounded-xl" placeholder="diabetes, high-value" />
                  </div>
                  <div>
                    <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1.5">Departments</label>
                    <div className="flex gap-1.5 flex-wrap">
                      {departments.map((d) => (
                        <button
                          key={d}
                          type="button"
                          data-testid={`nc-dept-${d}`}
                          onClick={() => toggle("departments", d)}
                          className={cn(
                            "text-ui-label px-2 py-1 rounded-xl border",
                            form.departments.includes(d) ? "bg-[#064E3B] text-white border-[#064E3B]" : "border-white/60 text-text-secondary hover:bg-white/60",
                          )}
                        >
                          {d}
                        </button>
                      ))}
                    </div>
                  </div>
                  <div>
                    <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1.5">Lead Status</label>
                    <div className="flex gap-1.5 flex-wrap">
                      {STATUSES.map((s) => (
                        <button
                          key={s}
                          type="button"
                          onClick={() => toggle("statuses", s)}
                          className={cn(
                            "text-ui-label px-2 py-1 rounded-xl border capitalize",
                            form.statuses.includes(s) ? "bg-[#064E3B] text-white border-[#064E3B]" : "border-white/60 text-text-secondary hover:bg-white/60",
                          )}
                        >
                          {s.replace(/_/g, " ")}
                        </button>
                      ))}
                    </div>
                  </div>
                  <div>
                    <label className="text-ui-label uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1.5">Inactive for (days)</label>
                    <Input type="number" value={form.inactive_days} onChange={(e) => setForm({ ...form, inactive_days: e.target.value })} className="rounded-xl max-w-[200px]" placeholder="e.g. 90" />
                  </div>
                </div>
                <Button type="button" variant="outline" size="sm" data-testid="nc-preview-btn" onClick={previewAudience} disabled={previewing} className="mt-3 rounded-xl border-[#064E3B]/30 text-[#064E3B] hover:bg-primary-soft">
                  <Users weight="regular" className="w-3.5 h-3.5 mr-1.5" />
                  {previewing ? "Calculating..." : "Preview audience"}
                </Button>
                {preview && (
                  <div className="mt-3 p-3 bg-primary-soft/80 border border-[#064E3B]/15 rounded-xl text-ui-base text-[#022C22] font-medium">
                    {preview.count} patient{preview.count !== 1 ? "s" : ""} match this audience
                  </div>
                )}
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setOpen(false)} className="rounded-xl">Cancel</Button>
              <Button data-testid="nc-save-btn" onClick={create} className="btn-primary">
                {form.auto_send ? "Schedule Campaign" : "Create Draft"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    >
      <PageContent wide fill flush className="grid grid-rows-[auto_minmax(0,1fr)] gap-2 min-h-0 overflow-hidden pb-2 p-2 sm:p-3">
        {/* Suggested campaigns carousel */}
        <section data-testid="suggested-drafts-section" className="shrink-0 min-w-0 overflow-hidden">
          <div className="flex items-center justify-between gap-2 mb-1.5">
            <div className="min-w-0">
              <h3 className="font-heading text-ui-base font-semibold text-[#022C22] flex items-center gap-1.5">
                <Sparkle weight="duotone" className="w-3.5 h-3.5 text-[#6366F1] shrink-0" />
                Suggested Campaigns
              </h3>
              <p className="text-ui-caption text-text-secondary truncate">Timely ideas from your marketing calendar</p>
            </div>
            {drafts.length > 0 && (
              <div className="flex gap-1 shrink-0">
                <button type="button" onClick={() => scrollCarousel(-1)} className="p-1 rounded-xl border border-white/60 bg-white/50 hover:bg-white/70" aria-label="Scroll suggested campaigns left">
                  <CaretLeft weight="bold" className="w-3.5 h-3.5" />
                </button>
                <button type="button" onClick={() => scrollCarousel(1)} className="p-1 rounded-xl border border-white/60 bg-white/50 hover:bg-white/70" aria-label="Scroll suggested campaigns right">
                  <CaretRight weight="bold" className="w-3.5 h-3.5" />
                </button>
              </div>
            )}
          </div>
          {drafts.length === 0 ? (
            <GlassCard className="text-center text-ui-sm text-text-muted py-4">No upcoming events in the marketing calendar.</GlassCard>
          ) : (
            <div ref={carouselRef} className="flex gap-2 overflow-x-auto pb-0.5 snap-x snap-mandatory scrollbar-thin max-w-full">
              {drafts.map((d) => (
                <SuggestedDraftCard key={d.event_id} draft={d} onUse={openFromDraft} />
              ))}
            </div>
          )}
        </section>

        {/* All campaigns — Kanban by status */}
        <section className="min-h-0 flex flex-col gap-1.5 overflow-hidden">
          <div className="shrink-0 flex flex-wrap items-center justify-between gap-2">
            <h3 className="font-heading text-ui-base font-semibold text-[#022C22]">All Campaigns</h3>
            <Input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search campaigns..."
              className="max-w-[200px] h-9 rounded-xl text-ui-sm glass-input border-white/60"
            />
          </div>

          {filteredRows.length === 0 ? (
            <GlassCard className="flex-1 min-h-0 flex flex-col items-center justify-center py-12 text-center">
              <Megaphone weight="duotone" className="w-10 h-10 mb-2 text-text-muted/50" />
              <div className="font-heading text-ui-base text-[#022C22] mb-1">No campaigns yet</div>
              <div className="text-ui-sm text-text-secondary">Create a campaign or use a suggested draft to get started.</div>
            </GlassCard>
          ) : (
            <GlassCard padding={false} className="flex-1 min-h-0 overflow-hidden p-2 flex flex-col" data-testid="campaigns-kanban">
              <KanbanBoard
                fillHeight
                className="h-full min-h-0 flex-1"
                columns={KANBAN_COLUMNS}
                items={filteredRows}
                getColumnId={statusToColumn}
                getItemKey={(c) => c.id}
                renderCard={(c) => (
                  <CampaignCard
                    campaign={c}
                    themeIndex={c.name?.length || 0}
                    onOpenMenu={() => navigate(`/campaigns/${c.id}`)}
                  />
                )}
                onMove={(c, col) => {
                  if (statusToColumn(c) === "sent") return;
                  handleKanbanMove(c, col);
                }}
                emptyLabel="Drop campaign here"
                maxVisible={50}
              />
            </GlassCard>
          )}
        </section>
      </PageContent>
    </AppShell>
  );
};

export default Campaigns;

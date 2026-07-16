import React, { useEffect, useState } from "react";
import { useParams, Link } from "@/lib/navigation";
import AppShell from "@/components/layout/AppShell";
import { api, formatPhone } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { ArrowLeft, PaperPlaneRight, Trash, Users, CheckCircle, XCircle, ChatCircle, Clock, CalendarBlank } from "@phosphor-icons/react";
import { toast } from "sonner";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import StatCard from "@/components/glass/StatCard";
import StatusPill from "@/components/glass/StatusPill";

const RECIPIENT_COLORS = {
  queued: "text-text-muted",
  sent: "text-emerald-700",
  delivered: "text-blue-700",
  read: "text-blue-700",
  failed: "text-red-700",
  replied: "text-purple-700",
};

const formatScheduled = (iso) => {
  if (!iso) return null;
  return new Date(iso).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
};

const WhatsAppPreview = ({ body }) => (
  <div className="rounded-xl bg-[#DCF8C6]/90 border border-[#16A34A]/25 px-4 py-3 text-[13px] text-[#022C22] whitespace-pre-wrap max-w-xl shadow-sm">
    {body}
  </div>
);

const CampaignDetail = () => {
  const { id } = useParams();
  const [data, setData] = useState(null);
  const [sending, setSending] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [scheduledAt, setScheduledAt] = useState("");
  const [scheduling, setScheduling] = useState(false);

  const load = () => api.get(`/campaigns/${id}`).then((r) => setData(r.data)).catch(() => setData(null));

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [id]);

  const send = async () => {
    setSending(true);
    try {
      const r = await api.post(`/campaigns/${id}/send`);
      toast.success(`Sent to ${r.data.sent} patients · ${r.data.failed} failed`);
      setConfirmOpen(false);
      load();
    } catch (e) { toast.error(e.response?.data?.detail || "Send failed"); }
    finally { setSending(false); }
  };

  const schedule = async () => {
    if (!scheduledAt) { toast.error("Pick date and time"); return; }
    setScheduling(true);
    try {
      await api.post(`/campaigns/${id}/schedule`, { scheduledAt: new Date(scheduledAt).toISOString() });
      toast.success(`Auto-send scheduled for ${formatScheduled(scheduledAt)}`);
      setScheduleOpen(false);
      load();
    } catch (e) { toast.error(e.response?.data?.detail || "Schedule failed"); }
    finally { setScheduling(false); }
  };

  const clearSchedule = async () => {
    try {
      await api.patch(`/campaigns/${id}`, { clearSchedule: true });
      toast.success("Auto-send cancelled — campaign is now a draft");
      load();
    } catch (e) { toast.error(e.response?.data?.detail || "Failed"); }
  };

  const remove = async () => {
    await api.delete(`/campaigns/${id}`);
    toast.success("Deleted");
    window.location.href = "/campaigns";
  };

  if (!data) return <AppShell title="Loading...">{null}</AppShell>;

  const c = data.campaign;
  const recipients = data.recipients;
  const audience = data.audience ?? c.audience ?? (c.audience_json ? JSON.parse(c.audience_json) : null);
  const canEdit = c.status === "draft" || c.status === "scheduled";

  return (
    <AppShell
      title={c.name}
      subtitle={c.description || "Campaign details"}
      actions={
        <div className="flex items-center gap-2 flex-wrap">
          <Link to="/campaigns" className="text-[13px] text-text-secondary hover:text-[#064E3B] flex items-center gap-1.5 rounded-xl px-2 py-1">
            <ArrowLeft weight="regular" className="w-4 h-4" /> Back
          </Link>
          {c.status === "draft" && (
            <>
              <Button
                variant="outline"
                onClick={() => { setScheduledAt(""); setScheduleOpen(true); }}
                className="rounded-xl h-9 text-[13px] border-blue-200 text-blue-700 hover:bg-blue-50"
                data-testid="schedule-campaign-btn"
              >
                <Clock weight="regular" className="w-3.5 h-3.5 mr-1.5" /> Schedule auto-send
              </Button>
              <Button
                data-testid="send-campaign-btn"
                onClick={() => setConfirmOpen(true)}
                className="rounded-xl bg-[#16A34A] hover:bg-[#15803D] text-white h-9 text-[13px]"
              >
                <PaperPlaneRight weight="fill" className="w-3.5 h-3.5 mr-1.5" /> Send Now
              </Button>
            </>
          )}
          {c.status === "scheduled" && (
            <Button
              variant="outline"
              onClick={clearSchedule}
              className="rounded-xl h-9 text-[13px]"
              data-testid="cancel-schedule-btn"
            >
              Cancel auto-send
            </Button>
          )}
        </div>
      }
    >
      <PageContent wide>
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="space-y-5">
            <GlassCard>
              <div className="flex items-center justify-between mb-4 gap-3 flex-wrap">
                <div className="flex flex-wrap items-center gap-2">
                  <StatusPill status={c.status} />
                  {c.status === "scheduled" && c.scheduled_at && (
                    <span className="inline-flex items-center gap-1.5 text-[12px] text-blue-700 bg-blue-50/80 border border-blue-200/60 rounded-xl px-2 py-0.5">
                      <Clock weight="fill" className="w-3.5 h-3.5" />
                      Auto-send {formatScheduled(c.scheduled_at)}
                    </span>
                  )}
                  {c.sent_at && (
                    <span className="text-[12px] text-text-muted">
                      Sent {formatScheduled(c.sent_at)}
                    </span>
                  )}
                </div>
                {canEdit && (
                  <button type="button" onClick={remove} className="text-text-muted hover:text-red-700 p-1 rounded-lg hover:bg-white/50" aria-label="Delete campaign">
                    <Trash className="w-4 h-4" />
                  </button>
                )}
              </div>
              {c.status === "scheduled" && c.scheduled_at && (
                <div className="flex items-start gap-3 p-3 rounded-xl bg-blue-50/80 border border-blue-200/60 text-[13px] text-blue-900">
                  <CalendarBlank weight="duotone" className="w-5 h-5 shrink-0 mt-0.5" />
                  <div>
                    <div className="font-medium">Scheduled for automatic delivery</div>
                    <div className="text-[12px] text-blue-800 mt-0.5">
                      This campaign will send via WhatsApp on <strong>{formatScheduled(c.scheduled_at)}</strong> without manual action.
                    </div>
                  </div>
                </div>
              )}
            </GlassCard>

            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              <StatCard icon={Users} label="Audience" value={c.total_recipients} />
              <StatCard icon={PaperPlaneRight} label="Sent" value={c.sent_count} accent="bg-emerald-50 text-emerald-700" />
              <StatCard icon={CheckCircle} label="Delivered" value={c.delivered_count} accent="bg-blue-50 text-blue-700" />
              <StatCard icon={ChatCircle} label="Replied" value={c.replied_count} accent="bg-violet-50 text-purple-700" />
              <StatCard icon={XCircle} label="Failed" value={c.failed_count} accent="bg-red-50 text-red-700" />
            </div>

            <GlassCard>
              <div className="text-[11px] uppercase tracking-[0.08em] text-text-secondary font-semibold mb-3">Message Body</div>
              <WhatsAppPreview body={c.message_body} />
            </GlassCard>

            <GlassCard>
              <div className="text-[11px] uppercase tracking-[0.08em] text-text-secondary font-semibold mb-3">Audience Criteria</div>
              <div className="grid grid-cols-2 gap-4 text-[13px]">
                <Criterion label="Tags" items={audience?.tags} />
                <Criterion label="Departments" items={audience?.departments} />
                <Criterion label="Statuses" items={audience?.statuses} />
                <Criterion label="Inactive (days)" items={audience?.inactive_days ? [String(audience.inactive_days)] : []} />
              </div>
            </GlassCard>
          </div>

          <GlassCard padding={false} className="flex flex-col min-h-[400px] lg:min-h-0">
            <div className="px-5 py-4 border-b border-white/50 shrink-0">
              <h3 className="font-heading text-[15px] font-semibold text-[#022C22]">Recipients ({recipients.length})</h3>
            </div>
            <div className="flex-1 max-h-[600px] lg:max-h-[calc(100vh-12rem)] overflow-y-auto scrollbar-thin">
              {recipients.length === 0 ? (
                <div className="py-10 text-center text-text-muted text-[13px]">No recipients yet · campaign not sent</div>
              ) : (
                <table className="w-full table-dense">
                  <thead className="bg-white/40 border-b border-white/50 sticky top-0 backdrop-blur-sm">
                    <tr className="text-left">
                      <th className="text-[10px] uppercase tracking-[0.08em] font-semibold text-text-secondary px-4 py-2">Patient</th>
                      <th className="text-[10px] uppercase tracking-[0.08em] font-semibold text-text-secondary px-4 py-2">Phone</th>
                      <th className="text-[10px] uppercase tracking-[0.08em] font-semibold text-text-secondary px-4 py-2">Status</th>
                      <th className="text-[10px] uppercase tracking-[0.08em] font-semibold text-text-secondary px-4 py-2">Sent At</th>
                    </tr>
                  </thead>
                  <tbody>
                    {recipients.map((r) => (
                      <tr key={r.id} className="border-b border-white/40 last:border-0 hover:bg-white/30">
                        <td className="text-[#022C22] px-4 py-2">{r.patient_name}</td>
                        <td className="font-mono text-text-secondary px-4 py-2">{formatPhone(r.patient_phone)}</td>
                        <td className={`capitalize px-4 py-2 ${RECIPIENT_COLORS[r.status]}`}>{r.status}</td>
                        <td className="text-text-muted px-4 py-2">{r.sent_at ? formatScheduled(r.sent_at) : "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </GlassCard>
        </div>
      </PageContent>

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent className="rounded-2xl glass-card" data-testid="confirm-send-dialog">
          <DialogHeader>
            <DialogTitle className="font-heading">Send campaign to {c.total_recipients} patients?</DialogTitle>
          </DialogHeader>
          <div className="text-[13px] text-text-secondary">
            This will deliver the WhatsApp message to <span className="font-semibold text-[#022C22]">{c.total_recipients}</span> patients immediately.
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)} className="rounded-xl">Cancel</Button>
            <Button data-testid="confirm-send-btn" onClick={send} disabled={sending} className="rounded-xl bg-[#16A34A] hover:bg-[#15803D]">
              {sending ? "Sending..." : "Yes, send now"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={scheduleOpen} onOpenChange={setScheduleOpen}>
        <DialogContent className="rounded-2xl glass-card" data-testid="schedule-dialog">
          <DialogHeader>
            <DialogTitle className="font-heading">Schedule auto-send</DialogTitle>
          </DialogHeader>
          <p className="text-[13px] text-text-secondary">
            The campaign will send automatically at the chosen date and time. You can cancel before it runs.
          </p>
          <div>
            <label className="text-[11px] uppercase tracking-[0.08em] text-text-secondary font-semibold block mb-1">
              Send at (local time)
            </label>
            <Input
              type="datetime-local"
              data-testid="schedule-datetime"
              value={scheduledAt}
              onChange={(e) => setScheduledAt(e.target.value)}
              className="rounded-xl max-w-xs"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setScheduleOpen(false)} className="rounded-xl">Cancel</Button>
            <Button onClick={schedule} disabled={scheduling} className="rounded-xl bg-blue-700 hover:bg-blue-800 text-white">
              {scheduling ? "Saving..." : "Enable auto-send"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AppShell>
  );
};

const Criterion = ({ label, items }) => (
  <div>
    <div className="text-[10px] uppercase tracking-[0.08em] text-text-muted font-semibold mb-1">{label}</div>
    {items && items.length > 0 ? (
      <div className="flex gap-1 flex-wrap">
        {items.map((it) => (
          <span key={it} className="text-[11px] bg-white/60 text-text-secondary px-2 py-0.5 rounded-full border border-white/70 capitalize">
            {String(it).replace(/_/g, " ")}
          </span>
        ))}
      </div>
    ) : <span className="text-text-muted text-[12px]">All</span>}
  </div>
);

export default CampaignDetail;

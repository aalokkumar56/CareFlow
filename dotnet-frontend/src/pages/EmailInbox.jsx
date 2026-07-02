import React, { useEffect, useState } from "react";
import AppShell from "@/components/layout/AppShell";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import { api, normalizeApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { MagnifyingGlass, EnvelopeSimple } from "@phosphor-icons/react";
import { cn } from "@/lib/utils";
import { toast } from "sonner";

const EmailInbox = () => {
  const [threads, setThreads] = useState([]);
  const [status, setStatus] = useState(null);
  const [search, setSearch] = useState("");
  const [selectedPatientId, setSelectedPatientId] = useState(null);
  const [threadDetail, setThreadDetail] = useState(null);
  const [compose, setCompose] = useState({ subject: "", body: "" });
  const [sending, setSending] = useState(false);

  const emailActive = status?.enabled && status?.is_configured;
  const emailDisabled = status && !emailActive;

  const loadThreads = ({ skipGlobalLoader = false } = {}) => {
    const qs = search ? `?q=${encodeURIComponent(search)}` : "";
    api.get(`/email/threads${qs}`, { skipGlobalLoader })
      .then((r) => {
        setThreads(r.data.threads || []);
        setStatus(r.data.status || null);
      })
      .catch((err) => toast.error(normalizeApiError(err, "Failed to load email threads")));
  };

  useEffect(() => {
    loadThreads();
    const timer = setInterval(() => loadThreads({ skipGlobalLoader: true }), 15000);
    return () => clearInterval(timer);
    /* eslint-disable-next-line */
  }, [search]);

  useEffect(() => {
    if (!selectedPatientId) {
      setThreadDetail(null);
      return;
    }
    api.get(`/email/threads/${selectedPatientId}`)
      .then((r) => setThreadDetail(r.data))
      .catch((err) => toast.error(normalizeApiError(err, "Failed to load thread")));
  }, [selectedPatientId]);

  const sendEmail = async (e) => {
    e.preventDefault();
    if (!selectedPatientId || !compose.body.trim()) return;
    setSending(true);
    try {
      await api.post("/email/send", {
        patientId: selectedPatientId,
        subject: compose.subject || "Message from Cure & Care Hospital",
        body: compose.body,
      });
      toast.success("Email sent");
      setCompose({ subject: "", body: "" });
      const r = await api.get(`/email/threads/${selectedPatientId}`);
      setThreadDetail(r.data);
      loadThreads();
    } catch (err) {
      toast.error(normalizeApiError(err, "Failed to send email"));
    } finally {
      setSending(false);
    }
  };

  return (
    <AppShell
      title="Email Inbox"
      subtitle="Patient email threads and outbound messages"
      hideHeaderSearch
      hideHospitalBadge
      showDate={false}
      scrollable={false}
      compactFooter
      wide
    >
      <PageContent wide fill flush className="flex min-h-0 h-full p-2 sm:p-3 flex-col">
        {emailDisabled && (
          <div className="mb-2 rounded-lg border border-amber-200/80 bg-amber-50/80 px-3 py-2 text-ui-sm text-amber-900 shrink-0">
            {status?.message || "Email is not configured or disabled. Configure it under Settings → Integrations."}
          </div>
        )}

        <div className="flex flex-1 gap-2 sm:gap-3 min-h-0 w-full overflow-hidden">
          <GlassCard padding={false} className="w-[280px] sm:w-[300px] shrink-0 flex flex-col min-h-0 overflow-hidden">
            <div className="shrink-0 p-2.5 sm:p-3 border-b border-white/50">
              <div className="relative">
                <MagnifyingGlass weight="regular" className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-text-muted pointer-events-none" />
                <Input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Search email threads..."
                  className="pl-8 rounded-lg h-8 text-ui-base glass-input border-white/60"
                />
              </div>
            </div>
            <div className="flex-1 min-h-0 overflow-y-auto scrollbar-thin">
              {threads.length === 0 && (
                <div className="p-8 text-center text-text-muted text-ui-base">No email threads yet</div>
              )}
              {threads.map((t) => (
                <button
                  key={t.patient_id}
                  type="button"
                  onClick={() => setSelectedPatientId(t.patient_id)}
                  className={cn(
                    "w-full text-left px-3 py-2.5 border-b border-white/40 hover:bg-white/50 transition-colors",
                    selectedPatientId === t.patient_id && "bg-primary-soft/80 border-l-2 border-l-[#4338CA]",
                  )}
                >
                  <div className="font-medium text-ui-base text-[#022C22] truncate">{t.patient_name}</div>
                  <div className="text-[10px] text-text-muted truncate">{t.to_email}</div>
                  <div className="text-ui-sm text-text-secondary truncate mt-0.5">{t.last_preview || "—"}</div>
                </button>
              ))}
            </div>
          </GlassCard>

          <GlassCard padding={false} className="flex-1 flex flex-col min-h-0 min-w-0 overflow-hidden">
            {!selectedPatientId || !threadDetail ? (
              <div className="flex-1 flex items-center justify-center text-text-muted">
                <div className="text-center">
                  <EnvelopeSimple weight="duotone" className="w-12 h-12 mx-auto mb-3 text-text-muted/50" />
                  <div className="text-ui-base">Select a thread to view messages</div>
                </div>
              </div>
            ) : (
              <>
                <div className="border-b border-white/50 px-4 py-3 shrink-0">
                  <div className="font-medium text-[14px] text-[#022C22]">{threadDetail.patient_name}</div>
                  <div className="text-[11px] text-text-secondary">{threadDetail.to_email}</div>
                </div>
                <div className="flex-1 min-h-0 overflow-y-auto scrollbar-thin px-4 py-3 space-y-3">
                  {(threadDetail.messages || []).map((m) => (
                    <div key={m.id} className="rounded-lg border border-white/60 bg-white/50 p-3">
                      <div className="flex items-center justify-between gap-2 mb-1">
                        <span className="text-ui-sm font-medium text-[#022C22]">{m.subject || "(no subject)"}</span>
                        <span className={cn(
                          "text-[10px] px-1.5 py-0.5 rounded-full",
                          m.status === "sent" ? "bg-emerald-100 text-emerald-700" : m.status === "failed" ? "bg-red-100 text-red-700" : "bg-gray-100 text-gray-600",
                        )}>
                          {m.status}
                        </span>
                      </div>
                      <div className="text-ui-sm text-text-secondary whitespace-pre-wrap">{m.body}</div>
                      <div className="text-[10px] text-text-muted mt-2">
                        {new Date(m.created_at).toLocaleString()}
                      </div>
                    </div>
                  ))}
                </div>
                <form onSubmit={sendEmail} className="border-t border-white/50 p-3 shrink-0 space-y-2">
                  <Input
                    value={compose.subject}
                    onChange={(e) => setCompose({ ...compose, subject: e.target.value })}
                    placeholder="Subject"
                    className="rounded-lg h-8 text-ui-sm"
                    disabled={!emailActive}
                  />
                  <Textarea
                    value={compose.body}
                    onChange={(e) => setCompose({ ...compose, body: e.target.value })}
                    placeholder={emailActive ? "Compose email..." : "Email sending is disabled"}
                    rows={3}
                    className="rounded-lg text-ui-sm resize-none"
                    disabled={!emailActive}
                  />
                  <Button type="submit" disabled={!emailActive || sending || !compose.body.trim()} className="btn-primary rounded-lg">
                    Send email
                  </Button>
                </form>
              </>
            )}
          </GlassCard>
        </div>
      </PageContent>
    </AppShell>
  );
};

export default EmailInbox;

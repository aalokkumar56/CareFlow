import React, { useEffect, useState } from "react";
import AppShell from "@/components/layout/AppShell";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import StatusPill from "@/components/glass/StatusPill";
import { api, normalizeApiError, CATEGORY_LABELS } from "@/lib/api";
import { fetchTemplates } from "@/lib/templatesCache";
import { unwrapPaged, buildPageQuery } from "@/lib/pagination";
import WhatsAppChatPanel from "@/components/WhatsAppChatPanel";
import { cn } from "@/lib/utils";
import { ChatCircleDots, MagnifyingGlass, WarningCircle } from "@phosphor-icons/react";
import { Input } from "@/components/ui/input";

const Inbox = () => {
  const [conversations, setConversations] = useState([]);
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState(null);
  const [templates, setTemplates] = useState([]);
  const [waStatus, setWaStatus] = useState(null);

  const whatsappDisabled = waStatus && (!waStatus.enabled || !waStatus.is_configured);

  const fetchConvs = ({ skipGlobalLoader = false } = {}) => {
    const qs = buildPageQuery({ q: search, page: 1, page_size: 100 });
    api.get(`/conversations?${qs}`, { skipGlobalLoader })
      .then((r) => setConversations(unwrapPaged(r).items))
      .catch((err) => {
        if (conversations.length === 0) {
          console.error(normalizeApiError(err, "Failed to load conversations"));
        }
      });
  };

  useEffect(() => {
    api.get("/settings/whatsapp/status")
      .then((r) => setWaStatus(r.data))
      .catch(() => setWaStatus({ enabled: false, is_configured: false, message: "Unable to load WhatsApp status." }));
  }, []);

  useEffect(() => {
    fetchConvs();
    const timer = setInterval(() => fetchConvs({ skipGlobalLoader: true }), 10000);
    return () => clearInterval(timer);
    /* eslint-disable-next-line */
  }, [search]);

  useEffect(() => {
    const controller = new AbortController();
    fetchTemplates({ signal: controller.signal }).then(setTemplates).catch(() => {});
    return () => controller.abort();
  }, []);

  const conversationName = (c) => c.display_name || c.name || c.wa_phone;

  return (
    <AppShell
      title="WhatsApp Inbox"
      subtitle="Multi-user shared inbox · real-time conversations"
      hideHeaderSearch
      hideHospitalBadge
      showDate={false}
      scrollable={false}
      compactFooter
      wide
    >
      <PageContent wide fill flush className="flex min-h-0 h-full p-2 sm:p-3 flex-col">
        {whatsappDisabled && (
          <div className="mb-2 rounded-lg border border-amber-200/80 bg-amber-50/80 px-3 py-2 text-ui-sm text-amber-900 flex items-start gap-2 shrink-0">
            <WarningCircle weight="fill" className="w-4 h-4 shrink-0 mt-0.5" />
            <span>{waStatus?.message || "WhatsApp messaging is disabled or not configured. Open Settings → Integrations to enable it."}</span>
          </div>
        )}
        <div className="flex flex-1 gap-2 sm:gap-3 min-h-0 w-full overflow-hidden">
          <GlassCard padding={false} className="w-[280px] sm:w-[300px] shrink-0 flex flex-col min-h-0 overflow-hidden">
            <div className="shrink-0 p-2.5 sm:p-3 border-b border-white/50">
              <div className="relative">
                <MagnifyingGlass weight="regular" className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-text-muted pointer-events-none" />
                <Input
                  data-testid="inbox-search"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Search conversations..."
                  className="pl-8 rounded-lg h-8 text-ui-base glass-input border-white/60"
                />
              </div>
            </div>
            <div className="flex-1 min-h-0 overflow-y-auto scrollbar-thin">
              {conversations.length === 0 && (
                <div className="p-8 text-center text-text-muted text-ui-base">No conversations yet</div>
              )}
              {conversations.map((c) => (
                <button
                  key={c.id}
                  data-testid={`conv-${c.id}`}
                  onClick={() => setSelectedId(c.id)}
                  className={cn(
                    "w-full text-left px-3 py-2.5 border-b border-white/40 hover:bg-white/50 transition-colors",
                    selectedId === c.id && "bg-primary-soft/80 border-l-2 border-l-[#064E3B]",
                  )}
                >
                  <div className="flex items-center justify-between gap-2 mb-0.5">
                    <div className="font-medium text-ui-base text-[#022C22] truncate">{conversationName(c)}</div>
                    {c.unread_count > 0 && (
                      <span className="badge-count bg-[#16A34A] text-white">
                        {c.unread_count}
                      </span>
                    )}
                  </div>
                  <div className="text-ui-sm text-text-secondary truncate mb-1">{c.last_message_preview || "—"}</div>
                  <div className="flex items-center gap-1 flex-wrap">
                    <StatusPill status={c.priority} />
                    <StatusPill
                      variant="muted"
                      label={CATEGORY_LABELS[c.category] || c.category}
                    />
                    {c.escalated && (
                      <StatusPill status="high" label="escalated" />
                    )}
                  </div>
                </button>
              ))}
            </div>
          </GlassCard>

          <GlassCard padding={false} className="flex-1 flex flex-col min-h-0 min-w-0 overflow-hidden">
            {!selectedId ? (
              <div className="flex-1 flex items-center justify-center text-text-muted">
                <div className="text-center">
                  <ChatCircleDots weight="duotone" className="w-12 h-12 mx-auto mb-3 text-text-muted/50" />
                  <div className="text-ui-base">Select a conversation to start</div>
                </div>
              </div>
            ) : (
              <WhatsAppChatPanel
                conversationId={selectedId}
                templates={templates}
                whatsappDisabled={whatsappDisabled}
                disabledMessage={waStatus?.message}
                className="flex-1 min-h-0 !border-0 !bg-transparent !rounded-none"
              />
            )}
          </GlassCard>
        </div>
      </PageContent>
    </AppShell>
  );
};

export default Inbox;

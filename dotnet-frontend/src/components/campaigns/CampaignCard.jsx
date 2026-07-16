import React from "react";
import { Link } from "@/lib/navigation";
import { WhatsappLogo, DotsThreeVertical, CalendarBlank, Users, Checks } from "@phosphor-icons/react";
import StatusPill from "@/components/glass/StatusPill";
import { cn } from "@/lib/utils";

const THEMES = [
  { panel: "bg-amber-50/50 backdrop-blur-sm", bubble: "bg-amber-50/60 backdrop-blur-sm border-white/60", tail: "bg-amber-50/60 border-white/60" },
  { panel: "bg-sky-50/50 backdrop-blur-sm", bubble: "bg-sky-50/60 backdrop-blur-sm border-white/60", tail: "bg-sky-50/60 border-white/60" },
  { panel: "bg-rose-50/50 backdrop-blur-sm", bubble: "bg-rose-50/60 backdrop-blur-sm border-white/60", tail: "bg-rose-50/60 border-white/60" },
  { panel: "bg-emerald-50/50 backdrop-blur-sm", bubble: "bg-emerald-50/60 backdrop-blur-sm border-white/60", tail: "bg-emerald-50/60 border-white/60" },
  { panel: "bg-violet-50/50 backdrop-blur-sm", bubble: "bg-violet-50/60 backdrop-blur-sm border-white/60", tail: "bg-violet-50/60 border-white/60" },
];

export const formatCampaignWhen = (iso) => {
  if (!iso) return null;
  const d = new Date(iso);
  return d.toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
};

const CampaignCard = ({ campaign, themeIndex = 0, onOpenMenu }) => {
  const theme = THEMES[themeIndex % THEMES.length];
  const when = formatCampaignWhen(campaign.scheduled_at || campaign.sent_at || campaign.created_at);
  const previewTime = campaign.scheduled_at
    ? new Date(campaign.scheduled_at).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
    : "11:30 AM";

  return (
    <div className={cn("w-full min-w-0 max-w-full rounded-xl border border-white/60 overflow-hidden shadow-sm", theme.panel)}>
      <div className="p-2 pb-1">
        <div className="flex items-center justify-between gap-1 mb-1">
          <WhatsappLogo weight="fill" className="w-3.5 h-3.5 text-[#25D366] shrink-0" />
          <button
            type="button"
            onClick={(e) => { e.preventDefault(); e.stopPropagation(); onOpenMenu?.(campaign); }}
            className="p-0.5 rounded-md text-slate-400 hover:text-slate-600 hover:bg-white/50 shrink-0"
            aria-label="Campaign options"
          >
            <DotsThreeVertical weight="bold" className="w-3.5 h-3.5" />
          </button>
        </div>
        <div className={cn("relative rounded-lg border px-2 py-1.5 text-ui-label text-[#022C22] line-clamp-2 break-words shadow-sm", theme.bubble)}>
          <span
            className={cn("absolute -left-1 top-2.5 w-1.5 h-1.5 border-l border-b rotate-45", theme.tail)}
            aria-hidden
          />
          {campaign.message_body || "Your message preview…"}
          <div className="mt-0.5 flex items-center justify-end gap-0.5 text-[9px] text-slate-400">
            <span>{previewTime}</span>
            <Checks weight="bold" className="w-2.5 h-2.5 text-sky-500" />
          </div>
        </div>
      </div>

      <Link
        to={`/campaigns/${campaign.id}`}
        className="block bg-white/55 backdrop-blur-sm px-2 py-1.5 border-t border-white/60 hover:bg-white/70 transition-colors min-w-0"
        draggable={false}
      >
        <div className="flex items-center justify-between gap-1.5 min-w-0">
          <div className="font-semibold text-ui-sm text-[#022C22] truncate min-w-0 flex-1">{campaign.name}</div>
          <StatusPill status={campaign.status} className="shrink-0" />
        </div>
        <div className="mt-1 flex items-center gap-2 text-ui-caption text-text-secondary min-w-0 overflow-hidden">
          {when && (
            <span className="flex items-center gap-0.5 shrink-0 min-w-0">
              <CalendarBlank weight="regular" className="w-3 h-3 shrink-0" />
              <span className="truncate">{when}</span>
            </span>
          )}
          <span className="flex items-center gap-0.5 shrink-0">
            <Users weight="regular" className="w-3 h-3 shrink-0" />
            {campaign.total_recipients ?? 0}
          </span>
        </div>
      </Link>
    </div>
  );
};

export const SuggestedDraftCard = ({ draft, onUse }) => {
  const theme = THEMES[(draft.name?.length || 0) % THEMES.length];
  const emoji = draft.category === "festival" ? "🪔" : draft.category === "seasonal" ? "☔" : draft.category === "health_day" ? "💚" : "📅";

  return (
    <GlassCardWrap className={cn("w-[210px] sm:w-[220px] shrink-0 snap-start", theme.panel)}>
      <div className="flex gap-2 p-2">
        <div className="w-10 h-10 rounded-lg bg-white/60 flex items-center justify-center text-xl shrink-0">
          {emoji}
        </div>
        <div className="min-w-0 flex-1">
          <div className="font-semibold text-ui-sm text-[#022C22] truncate">{draft.name}</div>
          <p className="text-ui-caption text-text-secondary line-clamp-1 mt-0.5">{draft.suggested_message}</p>
          <div className="text-[9px] text-text-muted mt-0.5 flex items-center gap-0.5">
            <CalendarBlank className="w-2.5 h-2.5" />
            {draft.event_date}
          </div>
        </div>
      </div>
      <div className="px-2 pb-2">
        <button
          type="button"
          onClick={() => onUse(draft)}
          className="w-full h-7 rounded-lg btn-primary text-ui-label font-medium"
          data-testid={`use-draft-${draft.event_id}`}
        >
          Use draft
        </button>
      </div>
    </GlassCardWrap>
  );
};

const GlassCardWrap = ({ children, className }) => (
  <div className={cn("rounded-xl border border-white/60 shadow-sm overflow-hidden", className)}>
    {children}
  </div>
);

export default CampaignCard;

import React from "react";
import { Link } from "react-router-dom";
import { Trophy, TrendUp, CurrencyInr } from "@phosphor-icons/react";
import SidePanel from "@/components/layout/SidePanel";
import { cn } from "@/lib/utils";
import { getReferrerAvatarColor, getReferrerInitials } from "./ReferrerCard";

const RANK_STYLES = [
  "text-amber-500",
  "text-slate-400",
  "text-amber-700",
];

const ReferralSidebar = ({ analytics, doctors = [] }) => {
  const topDoctors = (analytics.top_doctors || []).slice(0, 8).map((entry) => {
    const doc = doctors.find((d) => d.id === entry.doctor_id);
    return {
      ...entry,
      specialty: doc?.specialty || doc?.clinic || "",
    };
  });

  const totalReferrals = analytics.total_referrals ?? 0;
  const revenueLabel = analytics.total_revenue > 0
    ? `₹${(analytics.total_revenue / 1000).toFixed(1)}K`
    : "—";

  return (
    <SidePanel visible="xl" testId="referral-sidebar" className="mb-1">
      <div className="px-4 pt-4 pb-3 shrink-0 border-b border-white/45">
        <div className="flex items-center gap-2">
          <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-amber-400 to-orange-400 flex items-center justify-center shadow-sm">
            <Trophy weight="fill" className="text-white w-4 h-4" />
          </div>
          <div className="min-w-0">
            <div className="font-heading text-ui-sm font-bold text-[#022C22] truncate">Top Referrers</div>
            <div className="text-ui-caption text-text-muted">This month</div>
          </div>
        </div>
      </div>

      <div className="flex-1 min-h-0 overflow-y-auto scrollbar-thin px-2 py-2">
        {topDoctors.length === 0 ? (
          <p className="text-ui-caption text-text-muted text-center py-6 px-2">No referrals logged yet</p>
        ) : (
          <ul className="space-y-0.5">
            {topDoctors.map((entry, idx) => {
              const initials = getReferrerInitials(entry.doctor_name);
              const avatarColor = getReferrerAvatarColor(entry.doctor_name);
              return (
                <li key={entry.doctor_id}>
                  <Link
                    to={`/doctors/${entry.doctor_id}`}
                    className="flex items-center gap-2 px-2 py-2 rounded-xl hover:bg-white/35 transition-colors min-w-0"
                    title={entry.specialty || entry.doctor_name}
                  >
                    <span className={cn("w-4 text-center text-[10px] font-bold shrink-0", RANK_STYLES[idx] || "text-text-muted")}>
                      {idx + 1}
                    </span>
                    <div className={cn("w-7 h-7 rounded-full flex items-center justify-center text-[9px] font-bold shrink-0 ring-2 ring-white/50", avatarColor)}>
                      {initials}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="text-ui-caption font-semibold text-[#022C22] truncate">{entry.doctor_name}</div>
                      {entry.specialty && (
                        <div className="text-[10px] text-text-muted truncate">{entry.specialty}</div>
                      )}
                    </div>
                    <span className="text-ui-sm font-semibold text-[#022C22] tabular-nums shrink-0">{entry.count}</span>
                    {entry.revenue > 0 && (
                      <span className="text-ui-caption text-emerald-700 tabular-nums shrink-0">
                        ₹{Number(entry.revenue).toLocaleString("en-IN")}
                      </span>
                    )}
                  </Link>
                </li>
              );
            })}
          </ul>
        )}
      </div>

      <div className="border-t border-white/50 px-3 py-3 shrink-0 space-y-2">
        <div className="rounded-2xl bg-white/30 px-3 py-2.5 border border-white/45">
          <div className="text-[10px] uppercase tracking-wide text-text-muted">Total Referrals</div>
          <div className="flex items-end justify-between gap-2 mt-0.5">
            <div className="font-heading text-xl font-bold text-[#022C22] tabular-nums">{totalReferrals}</div>
            {totalReferrals > 0 && (
              <span className="inline-flex items-center gap-0.5 text-[10px] text-emerald-600 font-medium mb-0.5">
                <TrendUp weight="bold" className="w-3 h-3" />
                Active
              </span>
            )}
          </div>
        </div>
        <div className="rounded-2xl bg-white/30 px-3 py-2.5 border border-white/45 flex items-center gap-2">
          <CurrencyInr weight="duotone" className="w-4 h-4 text-emerald-600 shrink-0" />
          <div className="min-w-0">
            <div className="text-[10px] uppercase tracking-wide text-text-muted">Revenue</div>
            <div className="text-ui-sm font-bold text-[#022C22] tabular-nums truncate">{revenueLabel}</div>
          </div>
        </div>
      </div>
    </SidePanel>
  );
};

export default ReferralSidebar;

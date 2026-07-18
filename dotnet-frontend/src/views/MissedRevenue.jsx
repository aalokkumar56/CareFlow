import React, { useEffect, useMemo, useState } from "react";
import AppShell from "@/components/layout/AppShell";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import StatCard from "@/components/glass/StatCard";
import { api, formatPhone } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  Warning, ChatCircleDots, CalendarBlank, ListChecks, Clock, CurrencyInr, TrendUp,
} from "@phosphor-icons/react";
import { Link } from "@/lib/navigation";
import EmptyState from "@/components/ui/EmptyState";
import { useAuth } from "@/lib/auth";
import { formatHospitalDateTime, resolveHospitalTimezone } from "@/lib/tenantTime";

const formatRupee = (n) => `₹${Number(n || 0).toLocaleString("en-IN")}`;

const sectionSlug = (title) => title.toLowerCase().replace(/\s+/g, "-");

const Section = ({
  icon: Icon,
  title,
  subtitle,
  items,
  render,
  accent = "bg-primary-soft text-[#064E3B]",
  empty,
  categoryLoss,
}) => (
  <GlassCard
    padding={false}
    data-testid={`analytics-section-${sectionSlug(title)}`}
    className="overflow-hidden flex flex-col min-h-0 shadow-sm border-white/60"
  >
    <div className="px-3 py-2.5 border-b border-white/60 bg-white/45 backdrop-blur-sm flex items-center gap-2.5 shrink-0">
      <div className={cn("w-8 h-8 rounded-xl flex items-center justify-center shrink-0", accent)}>
        <Icon weight="regular" className="w-4 h-4" />
      </div>
      <div className="flex-1 min-w-0">
        <h3 className="font-heading text-ui-sm font-semibold text-[#022C22] leading-tight">{title}</h3>
        <p className="text-[10px] sm:text-ui-caption text-text-secondary leading-snug line-clamp-1">{subtitle}</p>
      </div>
      <div className="text-right shrink-0 flex flex-col items-end gap-0.5">
        <div className="text-[10px] sm:text-ui-caption font-semibold text-[#022C22] bg-white/60 border border-white/60 px-2 py-0.5 rounded-full tabular-nums">
          {items.length}
        </div>
        {categoryLoss > 0 && (
          <div
            className="text-[10px] sm:text-ui-caption font-medium text-red-700"
            data-testid={`analytics-loss-${sectionSlug(title)}`}
          >
            {formatRupee(categoryLoss)}
          </div>
        )}
      </div>
    </div>
    <div className="divide-y divide-white/50 flex-1 min-h-0 overflow-y-auto scrollbar-thin bg-white/10">
      {items.length === 0 && (
        <div className="py-3 sm:py-4 px-3 text-center text-[11px] sm:text-ui-sm text-text-secondary leading-snug">{empty}</div>
      )}
      {items.map((it, idx) => (
        <div key={it.id || idx} className="px-3 py-2.5 hover:bg-white/30 transition-colors">{render(it)}</div>
      ))}
    </div>
  </GlassCard>
);

const MissedRevenue = () => {
  const { tenant } = useAuth();
  const hospitalTz = resolveHospitalTimezone(tenant);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get("/dashboard/missed-revenue")
      .then((r) => setData(r.data))
      .catch(() => setData(null))
      .finally(() => setLoading(false));
  }, []);

  const totalItems = useMemo(() => {
    if (!data) return 0;
    return (
      (data.unanswered_inquiries?.length || 0)
      + (data.missed_appointments?.length || 0)
      + (data.lost_followups?.length || 0)
      + (data.inactive_patients?.length || 0)
    );
  }, [data]);

  const categoryTotals = data?.category_totals || {};

  const minutesSince = (iso) => {
    const ms = Date.now() - new Date(iso).getTime();
    const mins = Math.round(ms / 60000);
    if (mins < 60) return `${mins} min ago`;
    if (mins < 1440) return `${Math.round(mins / 60)} h ago`;
    return `${Math.round(mins / 1440)} d ago`;
  };

  return (
    <AppShell
      title="Analytics"
      subtitle="Missed revenue & operational accountability"
      hideHeaderSearch
      hideHospitalBadge
      showDate={false}
      scrollable={false}
      compactFooter
      wide
    >
      <PageContent wide fill flush className="flex flex-col min-h-0 gap-2 sm:gap-1.5 pb-1">
        {loading ? (
          <GlassCard className="text-center py-8 sm:py-10 text-ui-sm text-text-muted">Loading analytics…</GlassCard>
        ) : !data ? (
          <GlassCard className="text-center py-8 sm:py-10 text-ui-sm text-text-muted">Unable to load analytics</GlassCard>
        ) : (
          <>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-1 sm:gap-2 shrink-0">
              <StatCard
                icon={CurrencyInr}
                label="Total Estimated Loss"
                value={formatRupee(data.estimated_loss)}
                accent="bg-red-50 text-red-700"
                testId="analytics-total-loss"
                compact
              />
              <StatCard
                icon={Warning}
                label="Accountability Items"
                value={totalItems}
                accent="bg-amber-50 text-amber-700"
                testId="analytics-item-count"
                compact
              />
              <StatCard
                icon={TrendUp}
                label="Potentially Recoverable"
                value={formatRupee(data.recoverable_revenue)}
                accent="bg-emerald-50 text-emerald-700"
                testId="analytics-recoverable"
                compact
                className="col-span-2 sm:col-span-1"
              />
            </div>

            {totalItems === 0 ? (
              <GlassCard className="flex-1 flex items-center justify-center min-h-0">
                <EmptyState
                  testId="analytics-empty-state"
                  compact
                  illustration="/design/empty-state-analytics.svg"
                  title="All clear — no accountability items"
                  description="Great work. There are no unanswered inquiries, missed appointments, overdue follow-ups, or inactive patients right now."
                  action={(
                    <Link
                      to="/"
                      className="inline-flex items-center justify-center min-h-[44px] px-4 rounded-xl bg-[#064E3B] hover:bg-[#022C22] text-white text-ui-base font-medium transition-colors"
                    >
                      Back to dashboard
                    </Link>
                  )}
                />
              </GlassCard>
            ) : (
            <div className="flex-1 min-h-0 overflow-y-auto scrollbar-thin -mx-0.5 px-0.5 pb-2 sm:pb-1">
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-3 sm:gap-2 pb-1">
                <Section
                  icon={ChatCircleDots}
                  accent="bg-red-50 text-red-700"
                  title="Unanswered Inquiries"
                  subtitle="No staff reply for over 15 minutes"
                  empty="All caught up!"
                  items={data.unanswered_inquiries || []}
                  categoryLoss={categoryTotals.unanswered_inquiries?.estimated_loss}
                  render={(c) => (
                    <Link to="/inbox" className="block">
                      <div className="flex items-center justify-between gap-2">
                        <div className="min-w-0">
                          <div className="text-ui-sm text-[#022C22] font-medium truncate">{c.name || c.wa_phone}</div>
                          <div className="text-ui-caption text-text-muted line-clamp-1">{c.last_message_preview}</div>
                        </div>
                        <div className="text-right shrink-0">
                          <div className="text-ui-caption text-red-700 font-mono">{minutesSince(c.awaiting_reply_since)}</div>
                          <div className="text-ui-caption font-medium text-red-800">{formatRupee(c.estimated_loss)}</div>
                        </div>
                      </div>
                    </Link>
                  )}
                />

                <Section
                  icon={CalendarBlank}
                  accent="bg-orange-50 text-orange-700"
                  title="Missed Appointments"
                  subtitle="Patient did not arrive (no-show)"
                  empty="No missed appointments"
                  items={data.missed_appointments || []}
                  categoryLoss={categoryTotals.missed_appointments?.estimated_loss}
                  render={(a) => (
                    <div className="flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <div className="text-ui-sm text-[#022C22] font-medium truncate">{a.patient_name}</div>
                        <div className="text-ui-caption text-text-muted truncate">{a.doctor_name} · {a.department}</div>
                      </div>
                      <div className="text-right shrink-0">
                        <div className="text-ui-caption text-text-muted">{formatHospitalDateTime(a.scheduled_at, hospitalTz, "MMM d, yyyy, h:mm a")}</div>
                        <div className="text-ui-caption font-medium text-orange-800">{formatRupee(a.estimated_loss)}</div>
                      </div>
                    </div>
                  )}
                />

                <Section
                  icon={ListChecks}
                  accent="bg-amber-50 text-amber-700"
                  title="Overdue Follow-ups"
                  subtitle="Tasks past their due date"
                  empty="No overdue tasks"
                  items={data.lost_followups || []}
                  categoryLoss={categoryTotals.lost_followups?.estimated_loss}
                  render={(t) => (
                    <Link to="/tasks" className="block">
                      <div className="flex items-center justify-between gap-2">
                        <div className="min-w-0">
                          <div className="text-ui-sm text-[#022C22] font-medium truncate">{t.title}</div>
                          <div className="text-ui-caption text-text-muted truncate">{t.patient_name || "—"} · {t.type?.replace(/_/g, " ")}</div>
                        </div>
                        <div className="text-right shrink-0">
                          <div className="text-ui-caption text-amber-700">{t.due_at ? `due ${minutesSince(t.due_at)}` : ""}</div>
                          <div className="text-ui-caption font-medium text-amber-800">{formatRupee(t.estimated_loss)}</div>
                        </div>
                      </div>
                    </Link>
                  )}
                />

                <Section
                  icon={Clock}
                  accent="bg-purple-50 text-purple-700"
                  title="Inactive Patients"
                  subtitle="No contact in 90+ days · re-engage"
                  empty="All patients recently engaged"
                  items={data.inactive_patients || []}
                  categoryLoss={categoryTotals.inactive_patients?.estimated_loss}
                  render={(p) => (
                    <Link to={`/patients/${p.id}`} className="block">
                      <div className="flex items-center justify-between gap-2">
                        <div className="min-w-0">
                          <div className="text-ui-sm text-[#022C22] font-medium truncate">{p.name}</div>
                          <div className="text-ui-caption text-text-muted truncate">{formatPhone(p.phone)} · {p.department || "—"}</div>
                        </div>
                        <div className="text-right shrink-0">
                          <div className="text-ui-caption text-purple-700">{p.last_contact_at ? minutesSince(p.last_contact_at) : "never"}</div>
                          <div className="text-ui-caption font-medium text-purple-800">{formatRupee(p.estimated_loss)}</div>
                        </div>
                      </div>
                    </Link>
                  )}
                />
              </div>
            </div>
            )}
          </>
        )}
      </PageContent>
    </AppShell>
  );
};

export default MissedRevenue;

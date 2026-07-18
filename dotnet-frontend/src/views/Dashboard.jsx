import React, { useMemo, useState } from "react";
import AppShell from "@/components/layout/AppShell";
import CommandPalette from "@/components/layout/CommandPalette";
import DoctorDashboard from "@/views/DoctorDashboard";
import { useDashboardData } from "@/hooks/useDashboardData";
import { Link, useNavigate } from "@/lib/navigation";
import {
  CalendarBlank, UsersThree, CurrencyInr, Heart,
  Sparkle, CaretDown,
  MagnifyingGlass, ArrowSquareOut,
} from "@phosphor-icons/react";
import NotificationDropdown from "@/components/notifications/NotificationDropdown";
import {
  LineChart, Line, ResponsiveContainer, XAxis, YAxis, Tooltip, CartesianGrid, Area, AreaChart,
} from "recharts";
import DashboardStatCard from "@/components/glass/DashboardStatCard";
import StatCardsRow from "@/components/glass/StatCardsRow";
import DashboardDateTime from "@/components/glass/DashboardDateTime";
import GlassCard from "@/components/glass/GlassCard";
import ClientChart from "@/components/charts/ClientChart";
import { useAuth } from "@/lib/auth";
import usePermissions from "@/hooks/usePermissions";
import { normalizeRole, PERMISSIONS } from "@/lib/permissions";
import { useHospitalProfile } from "@/hooks/useHospitalProfile";
import { cn } from "@/lib/utils";
import { formatHospitalTime12h, resolveHospitalTimezone } from "@/lib/tenantTime";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

const APPT_PERIOD_OPTIONS = [
  { id: "this_week", label: "This Week", dataKey: "appointments_week" },
  { id: "last_week", label: "Last Week", dataKey: "appointments_last_week" },
];

const REVENUE_PERIOD_OPTIONS = [
  { id: "this_month", label: "This Month", dataKey: "revenue_month", totalKey: "revenue_mtd" },
  { id: "last_month", label: "Last Month", dataKey: "revenue_last_month", totalKey: "revenue_last_month_total" },
  { id: "this_week", label: "This Week", dataKey: "revenue_week", totalKey: null },
];

const formatRupee = (n) => `₹ ${Number(n || 0).toLocaleString("en-IN")}`;


const AVATAR_GRADIENTS = [
  "from-sky-300 to-blue-400",
  "from-violet-300 to-purple-400",
  "from-rose-300 to-pink-400",
];

const Dashboard = () => {
  const { user } = useAuth();
  if (normalizeRole(user?.role) === "doctor") {
    return <DoctorDashboard />;
  }

  return <OperationsDashboard />;
};

const OperationsDashboard = () => {
  const { user, tenant } = useAuth();
  const hospitalTz = resolveHospitalTimezone(tenant);
  const { can } = usePermissions();
  const showRevenue = can(PERMISSIONS.BillingView);
  const { hospitalName } = useHospitalProfile();
  const { overview: data, loading } = useDashboardData();
  const navigate = useNavigate();

  const [paletteOpen, setPaletteOpen] = useState(false);
  const [apptPeriod, setApptPeriod] = useState("this_week");
  const [revenuePeriod, setRevenuePeriod] = useState("this_month");

  const displayName = user?.name || "User";
  const isDoctor = normalizeRole(user?.role) === "doctor";
  const firstName = isDoctor
    ? (displayName.includes("Dr.") ? displayName : `Dr. ${displayName.split(" ")[0]}`)
    : displayName.replace(/^Dr\.\s*/i, "").split(" ")[0];

  const revenueConfig = useMemo(
    () => REVENUE_PERIOD_OPTIONS.find((o) => o.id === revenuePeriod) || REVENUE_PERIOD_OPTIONS[0],
    [revenuePeriod],
  );

  const appointmentsWeek = useMemo(() => {
    if (!data) return [];
    const opt = APPT_PERIOD_OPTIONS.find((o) => o.id === apptPeriod) || APPT_PERIOD_OPTIONS[0];
    const series = data[opt.dataKey];
    return Array.isArray(series) ? series : (Array.isArray(data.appointments_week) ? data.appointments_week : []);
  }, [data, apptPeriod]);

  const revenueWeek = useMemo(() => {
    if (!data) return [];
    const series = data[revenueConfig.dataKey];
    return Array.isArray(series) ? series : (Array.isArray(data.revenue_week) ? data.revenue_week : []);
  }, [data, revenueConfig]);

  const revenueDisplayTotal = useMemo(() => {
    if (!data) return 0;
    if (revenueConfig.totalKey && data[revenueConfig.totalKey] != null) {
      return Number(data[revenueConfig.totalKey]) || 0;
    }
    return revenueWeek.reduce((sum, d) => sum + (Number(d.amount) || 0), 0);
  }, [data, revenueConfig, revenueWeek]);

  const revenuePeriodLabel = revenuePeriod === "this_month"
    ? "Total Revenue (MTD)"
    : revenuePeriod === "last_month"
      ? "Total Revenue (Last Month)"
      : "Total Revenue (This Week)";

  if (!loading && !data) {
    return (
      <AppShell variant="dashboard">
        <div className="h-full flex items-center justify-center text-text-secondary text-ui-sm">Dashboard data could not be loaded.</div>
      </AppShell>
    );
  }

  const revenueChartMax = data
    ? Math.max(1, ...revenueWeek.map((d) => Number(d.amount) || 0))
    : 1;
  const upcoming = data && Array.isArray(data.upcoming_appointments) ? data.upcoming_appointments : [];

  const apptTrendPct = data?.appointments_today_change_pct ?? 12;
  const patientTrendPct = data?.new_patients_change_pct ?? 8;
  const revenueTrendPct = data?.revenue_mtd_change_pct ?? 15;

  return (
    <AppShell variant="dashboard">
      <div className="dashboard-page h-full px-3 sm:px-4 lg:px-5 pb-20 lg:pb-0 flex flex-col gap-2 sm:gap-3 overflow-y-auto lg:overflow-hidden">
        <header className="shrink-0 flex items-center justify-between gap-2 sm:justify-end">
          <DashboardDateTime className="flex sm:hidden text-[10px] px-2 py-1" />
          <DashboardDateTime className="hidden sm:flex" />
        </header>

        <div className="shrink-0 flex flex-col gap-3 sm:gap-4">
          <div className="grid grid-cols-1 sm:grid-cols-4 gap-2.5 items-center">
            <div className="sm:col-span-2 min-w-0">
              <h1
                className="font-heading text-lg sm:text-[20px] font-semibold tracking-tight text-[#022C22] leading-tight truncate"
                data-testid="page-title"
              >
                Good morning, {firstName} 👋
              </h1>
              <p className="text-ui-sm text-text-secondary mt-0.5 truncate">
                Here&apos;s what&apos;s happening at {hospitalName} today.
              </p>
            </div>

            <div className="col-span-2 hidden sm:flex items-center justify-end gap-2 min-h-8">
              <button
                data-testid="open-command-palette"
                onClick={() => setPaletteOpen(true)}
                aria-label="Open command palette"
                className="flex-1 max-w-[calc(100%-2.75rem)] ml-[25%] glass-input hover:bg-white/50 transition-colors flex items-center gap-2 px-3 py-1.5 text-ui-sm text-text-muted"
              >
                <MagnifyingGlass weight="regular" className="w-3.5 h-3.5 shrink-0" />
                <span className="flex-1 text-left truncate">Search patients, appointments...</span>
                <kbd className="font-mono text-[10px] bg-white/55 px-1.5 py-0.5 rounded hidden lg:inline">⌘K</kbd>
              </button>
              <NotificationDropdown className="p-1.5" iconClassName="w-4 h-4 text-indigo-700" />
            </div>
          </div>

          <div className="flex sm:hidden items-center gap-2">
            <button
              data-testid="open-command-palette-mobile"
              onClick={() => setPaletteOpen(true)}
              aria-label="Open command palette"
              className="flex-1 glass-input hover:bg-white/50 transition-colors flex items-center gap-2 px-3 py-2 text-ui-sm text-text-muted"
            >
              <MagnifyingGlass weight="regular" className="w-3.5 h-3.5 shrink-0" />
              <span className="flex-1 text-left truncate">Search patients, appointments...</span>
            </button>
            <NotificationDropdown className="p-2" iconClassName="w-4 h-4 text-indigo-700" />
          </div>

          {loading ? (
            <div className="grid grid-cols-2 xl:grid-cols-4 gap-2.5 animate-pulse">
              {[1, 2, 3, 4].map((i) => <div key={i} className="h-[88px] bg-white/25 rounded-2xl" />)}
            </div>
          ) : (
          <StatCardsRow columns={showRevenue ? 4 : 3}>
            <DashboardStatCard
              testId="stat-appointments"
              icon={CalendarBlank}
              label="Today's Appointments"
              value={data.appointments_today ?? 32}
              trend={`↑ ${Math.abs(apptTrendPct)}%`}
              trendLabel="vs yesterday"
              iconBg="bg-blue-100"
              iconColor="text-blue-500"
              href="/appointments?date=today"
            />
            <DashboardStatCard
              testId="stat-new-inquiries"
              icon={UsersThree}
              label="New Patients"
              value={data.new_inquiries_today ?? 18}
              trend={`↑ ${Math.abs(patientTrendPct)}%`}
              trendLabel="vs yesterday"
              iconBg="bg-emerald-100"
              iconColor="text-emerald-500"
              href="/patients?status=new_inquiry"
            />
            {showRevenue && (
              <DashboardStatCard
                testId="stat-revenue"
                icon={CurrencyInr}
                label="Revenue (MTD)"
                value={formatRupee(data.revenue_mtd ?? 1872500)}
                trend={`↑ ${Math.abs(revenueTrendPct)}%`}
                trendLabel="vs last month"
                iconBg="bg-orange-100"
                iconColor="text-amber-500"
                menuItems={(
                  <>
                    <DropdownMenuItem onClick={() => navigate("/missed-revenue")}>
                      <ArrowSquareOut className="w-3.5 h-3.5 mr-2" /> View analytics
                    </DropdownMenuItem>
                  </>
                )}
              />
            )}
            <DashboardStatCard
              testId="stat-care-score"
              icon={Heart}
              label="Care Score"
              value={`${data.care_score ?? 4.8}/5`}
              trend={`↑ ${data.care_score_change ?? 0.3}`}
              trendLabel="vs last month"
              iconBg="bg-pink-100"
              iconColor="text-pink-500"
            />
          </StatCardsRow>
          )}
        </div>

        <div className="flex-1 min-h-0">
        {loading ? (
          <div className="dashboard-main-grid h-full animate-pulse">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="bg-white/25 rounded-2xl min-h-[120px]" />
            ))}
          </div>
        ) : (
        <div className={cn("dashboard-main-grid h-full", !showRevenue && "dashboard-main-grid--clinical")}>
          <GlassCard variant="dashboard" padding={false} className="dashboard-cell-top-left px-3 pt-3 flex flex-col min-h-0" data-testid="dashboard-appointments-overview">
            <div className="flex items-center justify-between gap-2 shrink-0 mb-1">
              <div className="flex items-center gap-3 min-w-0">
                <h3 className="font-heading text-ui-sm font-semibold text-[#022C22] shrink-0">Appointments Overview</h3>
                <div className="hidden md:flex items-center gap-2.5 text-ui-caption text-text-secondary" data-testid="dashboard-appt-chart-legend">
                  <span className="flex items-center gap-1">
                    <span className="w-1.5 h-1.5 rounded-full bg-indigo-600" />Scheduled
                  </span>
                  <span className="flex items-center gap-1">
                    <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />Completed
                  </span>
                </div>
              </div>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <button
                    type="button"
                    data-testid="dashboard-appt-week-toggle"
                    className="flex items-center gap-0.5 text-ui-caption px-2 py-0.5 rounded-full bg-white/40 border border-white/55 text-text-secondary font-medium shrink-0"
                  >
                    {(APPT_PERIOD_OPTIONS.find((o) => o.id === apptPeriod) || APPT_PERIOD_OPTIONS[0]).label}
                    {" "}<CaretDown weight="bold" className="w-2.5 h-2.5" />
                  </button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="rounded-xl glass-card border-white/60 text-[11px]">
                  {APPT_PERIOD_OPTIONS.map((opt) => (
                    <DropdownMenuItem
                      key={opt.id}
                      data-testid={`dashboard-appt-period-${opt.id}`}
                      onClick={() => setApptPeriod(opt.id)}
                    >
                      {opt.label}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
            <div className="flex-1 min-h-[50px] min-w-0" data-testid="dashboard-appt-chart" key={apptPeriod}>
              <ClientChart className="h-full" minHeight={50}>
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={appointmentsWeek} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.1)" vertical={false} />
                    <XAxis dataKey="label" tick={{ fontSize: 10, fill: "#94A3B8" }} axisLine={false} tickLine={false} dy={6} />
                    <YAxis tick={{ fontSize: 10, fill: "#94A3B8" }} axisLine={false} tickLine={false} width={28} />
                    <Tooltip contentStyle={{ fontSize: 11, borderRadius: 10 }} />
                    <Line type="monotone" dataKey="scheduled" stroke="#4F46E5" strokeWidth={2} dot={false} />
                    <Line type="monotone" dataKey="completed" stroke="#10B981" strokeWidth={2} dot={false} />
                  </LineChart>
                </ResponsiveContainer>
              </ClientChart>
            </div>
          </GlassCard>

          <GlassCard variant="dashboard" padding={false} className="dashboard-cell-top-right px-3 pt-3 flex flex-col" data-testid="dashboard-upcoming-appointments">
            <div className="flex items-center justify-between mb-2 shrink-0">
              <h3 className="font-heading text-ui-sm font-semibold text-[#022C22]">Upcoming Appointments</h3>
              <Link to="/appointments" className="text-[10px] font-medium text-indigo-500 hover:underline" data-testid="dashboard-upcoming-view-all">View all</Link>
            </div>
            <div className="flex-1 min-h-[50px] overflow-hidden" data-testid="dashboard-upcoming-list">
              {upcoming.length === 0 && (
                <p className="text-ui-caption text-text-muted text-center py-4" data-testid="dashboard-upcoming-empty">No upcoming appointments</p>
              )}
              {upcoming.slice(0, 3).map((appt, idx) => {
                const isConfirmed = appt.status === "confirmed" || appt.status === "scheduled";
                return (
                  <div key={appt.id} data-testid={`dashboard-upcoming-row-${idx}`} className={cn("flex items-center gap-2 py-2", idx > 0 && "border-t border-white/35")}>
                    <div className="w-12 shrink-0 text-right">
                      <p className="text-ui-sm font-semibold text-[#022C22] leading-tight">{formatHospitalTime12h(appt.scheduled_at, hospitalTz)}</p>
                      <p className="text-ui-caption text-text-muted">{appt.duration_minutes || 30} min</p>
                    </div>
                    <div className={cn(
                      "w-7 h-7 rounded-full flex items-center justify-center text-white text-[10px] font-semibold shrink-0 bg-gradient-to-br ring-1 ring-white/60",
                      AVATAR_GRADIENTS[idx % AVATAR_GRADIENTS.length],
                    )}>
                      {appt.patient_name?.[0]?.toUpperCase() || "P"}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="text-ui-sm font-semibold text-[#022C22] truncate">{appt.patient_name}</p>
                      <p className="text-ui-caption text-text-secondary truncate">{appt.appointment_type || "Consultation"}</p>
                    </div>
                    <span className={cn(
                      "text-[9px] px-1.5 py-0.5 rounded-full font-semibold shrink-0",
                      isConfirmed ? "bg-emerald-100 text-emerald-700" : "bg-orange-100 text-amber-700",
                    )}>
                      {isConfirmed ? "Confirmed" : "Pending"}
                    </span>
                  </div>
                );
              })}
            </div>
          </GlassCard>

          {showRevenue && (
          <GlassCard variant="dashboard" padding={false} className="dashboard-cell-bottom-left p-3 flex flex-col h-full min-h-0 overflow-hidden" data-testid="dashboard-revenue-overview">
            <div className="dashboard-revenue-inner flex items-center gap-3 sm:gap-4 h-full min-h-0">
              <div className="shrink-0 flex flex-col justify-center max-w-[42%] sm:max-w-none">
                <div className="flex items-center justify-between gap-2 mb-0.5">
                  <h3 className="font-heading text-ui-sm font-semibold text-[#022C22]">Revenue Overview</h3>
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <button
                        type="button"
                        data-testid="dashboard-revenue-period-toggle"
                        className="flex items-center gap-0.5 text-ui-caption px-2 py-0.5 rounded-full bg-white/40 border border-white/55 text-text-secondary font-medium shrink-0"
                      >
                        {revenueConfig.label}
                        {" "}<CaretDown weight="bold" className="w-2.5 h-2.5" />
                      </button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end" className="rounded-xl glass-card border-white/60 text-[11px]">
                      {REVENUE_PERIOD_OPTIONS.map((opt) => (
                        <DropdownMenuItem
                          key={opt.id}
                          data-testid={`dashboard-revenue-period-${opt.id}`}
                          onClick={() => setRevenuePeriod(opt.id)}
                        >
                          {opt.label}
                        </DropdownMenuItem>
                      ))}
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>
                <p className="text-ui-caption text-text-muted leading-tight">{revenuePeriodLabel}</p>
                <p className="font-heading text-ui-stat font-semibold text-[#022C22] leading-none mt-0.5" data-testid="dashboard-revenue-mtd-value">
                  {formatRupee(revenueDisplayTotal)}
                </p>
                <p className="text-[10px] mt-0.5 leading-tight">
                  <span className="font-semibold text-emerald-500">↑ {Math.abs(revenueTrendPct)}%</span>
                  <span className="text-text-muted ml-1">vs last month</span>
                </p>
              </div>
              <div className="dashboard-revenue-chart" data-testid="dashboard-revenue-chart" key={revenuePeriod}>
                <ClientChart className="h-full" minHeight={50}>
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={revenueWeek} margin={{ top: 4, right: 4, left: 0, bottom: 0 }}>
                      <defs>
                        <linearGradient id="revGrad" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="0%" stopColor="#8B5CF6" stopOpacity={0.35} />
                          <stop offset="100%" stopColor="#8B5CF6" stopOpacity={0.04} />
                        </linearGradient>
                      </defs>
                      <YAxis hide domain={[0, revenueChartMax]} />
                      <Area type="monotone" dataKey="amount" stroke="#8B5CF6" strokeWidth={1.5} fill="url(#revGrad)" dot={false} />
                    </AreaChart>
                  </ResponsiveContainer>
                </ClientChart>
              </div>
            </div>
          </GlassCard>
          )}

          <GlassCard variant="dashboard" padding={false} className="dashboard-cell-bottom-right p-3 flex flex-col justify-center" id="ai-insights" data-testid="dashboard-ai-insights">
            <div className="flex items-start gap-2.5">
              <div className="w-8 h-8 rounded-xl bg-violet-100 flex items-center justify-center shrink-0">
                <Sparkle weight="fill" className="w-4 h-4 text-violet-600" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-1.5 mb-0.5">
                  <h3 className="font-heading text-ui-sm font-semibold text-[#022C22]">CareFlow AI Insights</h3>
                  <span className="text-[8px] uppercase px-1 py-0.5 rounded bg-violet-500 text-white font-bold">New</span>
                </div>
                <p className="text-ui-sm font-semibold text-[#022C22]" data-testid="dashboard-ai-insight-title">High no-show rate on Tuesdays</p>
                <p className="text-ui-caption text-text-secondary leading-snug line-clamp-2 mt-0.5" data-testid="dashboard-ai-insight-body">
                  Consider WhatsApp reminders 1 day before appointments.
                </p>
                <button
                  type="button"
                  data-testid="dashboard-view-insight"
                  onClick={() => navigate("/missed-revenue")}
                  className="mt-2 text-[10px] font-semibold px-3 py-1 rounded-full bg-white/50 border border-white/60 text-violet-600"
                >
                  View Insight
                </button>
              </div>
            </div>
          </GlassCard>
        </div>
        )}
        </div>
      </div>

      <CommandPalette open={paletteOpen} setOpen={setPaletteOpen} />
    </AppShell>
  );
};

export default Dashboard;

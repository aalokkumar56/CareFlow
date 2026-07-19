import React, { useMemo, useState } from "react";
import AppShell from "@/components/layout/AppShell";
import { useNavigate } from "@/lib/navigation";
import { useAuth } from "@/lib/auth";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";
import { useClinicalDashboardData } from "@/hooks/useClinicalDashboardData";
import DashboardStatCard from "@/components/glass/DashboardStatCard";
import StatCardsRow from "@/components/glass/StatCardsRow";
import GlassCard from "@/components/glass/GlassCard";
import StatusPill from "@/components/glass/StatusPill";
import PageContent from "@/components/glass/PageContent";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { api, normalizeApiError } from "@/lib/api";
import { toast } from "sonner";
import {
  CalendarBlank, CheckCircle, Clock, Stethoscope, UserCircle,
} from "@phosphor-icons/react";
import { cn } from "@/lib/utils";
import {
  formatHospitalTime12h,
  hospitalTodayDateStr,
  resolveHospitalTimezone,
} from "@/lib/tenantTime";

const STATUS_LABELS = {
  scheduled: "Scheduled",
  confirmed: "Checked in",
  completed: "Completed",
  cancelled: "Cancelled",
  no_show: "No-show",
};

const DoctorDashboard = () => {
  const { user, tenant } = useAuth();
  const hospitalTz = resolveHospitalTimezone(tenant);
  const { can } = usePermissions();
  const navigate = useNavigate();
  const canEditAppointment = can(PERMISSIONS.AppointmentEdit);

  const [selectedDate, setSelectedDate] = useState(() => hospitalTodayDateStr(hospitalTz));
  const [scope, setScope] = useState("mine");
  const [doctorFilter, setDoctorFilter] = useState("");

  const { overview, loading, reload } = useClinicalDashboardData({
    date: selectedDate,
    scope,
    doctorUserId: scope === "all" && doctorFilter ? doctorFilter : undefined,
  });

  const displayName = user?.name || "Doctor";
  const firstName = displayName.includes("Dr.")
    ? displayName.replace(/^Dr\.\s*/i, "").split(" ")[0]
    : displayName.split(" ")[0];

  const appointments = useMemo(
    () => (Array.isArray(overview?.appointments_today) ? overview.appointments_today : []),
    [overview],
  );

  const stats = overview?.stats || {};

  const handleCheckIn = async (appointmentId) => {
    try {
      await api.patch(`/appointments/${appointmentId}/status`, { status: "confirmed" });
      toast.success("Patient checked in");
      reload();
    } catch (error) {
      toast.error(normalizeApiError(error));
    }
  };

  const openChart = (appointment) => {
    navigate(`/patients/${appointment.patient_id}?tab=today&appointment=${appointment.id}`);
  };

  return (
    <AppShell
      title={`Good morning, Dr. ${firstName}`}
      subtitle="Today's clinical schedule"
      hideHeaderSearch
    >
      <PageContent className="space-y-4">
        <div className="flex flex-col lg:flex-row lg:items-center gap-3 justify-between">
          <div className="flex flex-wrap items-center gap-2">
            <Input
              type="date"
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
              className="w-[160px] rounded-xl h-10"
              data-testid="doctor-dashboard-date"
            />
            <div className="flex rounded-xl border border-white/50 bg-white/30 p-1">
              <button
                type="button"
                data-testid="doctor-scope-mine"
                onClick={() => { setScope("mine"); setDoctorFilter(""); }}
                className={cn(
                  "px-3 py-1.5 rounded-lg text-[13px] font-medium transition-colors",
                  scope === "mine" ? "bg-[#064E3B] text-white" : "text-[#4B5563] hover:bg-white/50",
                )}
              >
                My appointments
              </button>
              <button
                type="button"
                data-testid="doctor-scope-all"
                onClick={() => setScope("all")}
                className={cn(
                  "px-3 py-1.5 rounded-lg text-[13px] font-medium transition-colors",
                  scope === "all" ? "bg-[#064E3B] text-white" : "text-[#4B5563] hover:bg-white/50",
                )}
              >
                All doctors
              </button>
            </div>
            {scope === "all" && Array.isArray(overview?.doctors) && overview.doctors.length > 0 && (
              <Select value={doctorFilter || "all"} onValueChange={(v) => setDoctorFilter(v === "all" ? "" : v)}>
                <SelectTrigger className="w-[200px] rounded-xl h-10" data-testid="doctor-filter-select">
                  <SelectValue placeholder="All doctors" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All doctors</SelectItem>
                  {overview.doctors.map((d) => (
                    <SelectItem key={d.user_id} value={d.user_id}>{d.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
        </div>

        <StatCardsRow>
          <DashboardStatCard
            testId="doctor-stat-today"
            icon={CalendarBlank}
            label="Today's appointments"
            value={loading ? "…" : String(stats.appointments_today ?? 0)}
            iconBg="bg-indigo-50"
            iconColor="text-indigo-600"
            showMenu={false}
          />
          <DashboardStatCard
            testId="doctor-stat-completed"
            icon={CheckCircle}
            label="Completed"
            value={loading ? "…" : String(stats.completed_today ?? 0)}
            iconBg="bg-emerald-50"
            iconColor="text-emerald-600"
            showMenu={false}
          />
          <DashboardStatCard
            testId="doctor-stat-waiting"
            icon={Clock}
            label="Waiting check-in"
            value={loading ? "…" : String(stats.waiting_check_in ?? 0)}
            iconBg="bg-amber-50"
            iconColor="text-amber-600"
            showMenu={false}
          />
        </StatCardsRow>

        <GlassCard className="p-0 overflow-hidden" data-testid="doctor-appointment-queue">
          <div className="px-4 py-3 border-b border-white/40 flex items-center gap-2">
            <Stethoscope weight="duotone" className="w-5 h-5 text-[#064E3B]" />
            <h2 className="font-heading font-semibold text-[#022C22]">Appointment queue</h2>
          </div>

          {loading ? (
            <div className="p-8 text-center text-text-secondary text-sm">Loading schedule…</div>
          ) : appointments.length === 0 ? (
            <div className="p-8 text-center text-text-secondary text-sm" data-testid="doctor-queue-empty">
              No appointments for this day.
            </div>
          ) : (
            <div className="divide-y divide-white/30">
              {appointments.map((appt) => {
                const canConsult = appt.is_mine || canEditAppointment;
                return (
                  <div
                    key={appt.id}
                    className="px-4 py-3 flex flex-col sm:flex-row sm:items-center gap-3 hover:bg-white/20 transition-colors"
                    data-testid={`doctor-appt-row-${appt.id}`}
                    data-doctor-id={appt.doctor_user_id || ""}
                    data-is-mine={appt.is_mine ? "true" : "false"}
                  >
                    <div className="shrink-0 w-20 text-[13px] font-semibold text-[#064E3B]">
                      {formatHospitalTime12h(appt.scheduled_at, hospitalTz)}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-medium text-[#022C22]">{appt.patient_name}</span>
                        <StatusPill variant="neutral" label={STATUS_LABELS[appt.status] || appt.status} />
                      </div>
                      <div className="text-[12px] text-text-secondary mt-0.5 truncate">
                        {appt.doctor_name} · {appt.department}
                        {appt.patient_phone ? ` · ${appt.patient_phone}` : ""}
                      </div>
                      {appt.chief_complaint && (
                        <div className="text-[12px] text-text-muted mt-1 truncate">{appt.chief_complaint}</div>
                      )}
                    </div>
                    <div className="flex items-center gap-2 shrink-0">
                      {canEditAppointment && appt.status === "scheduled" && appt.is_mine && (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          className="rounded-xl"
                          data-testid={`doctor-checkin-${appt.id}`}
                          onClick={() => handleCheckIn(appt.id)}
                        >
                          Check in
                        </Button>
                      )}
                      {canConsult ? (
                        <Button
                          type="button"
                          size="sm"
                          className="rounded-xl bg-[#064E3B] hover:bg-[#022C22]"
                          data-testid={`doctor-open-chart-${appt.id}`}
                          onClick={() => openChart(appt)}
                        >
                          Open chart
                        </Button>
                      ) : (
                        <span className="text-[11px] text-text-muted flex items-center gap-1">
                          <UserCircle className="w-4 h-4" />
                          {appt.doctor_name}
                        </span>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </GlassCard>
      </PageContent>
    </AppShell>
  );
};

export default DoctorDashboard;

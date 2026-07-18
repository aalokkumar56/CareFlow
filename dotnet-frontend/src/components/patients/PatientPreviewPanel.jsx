import React, { useEffect, useMemo, useState } from "react";
import { Link } from "@/lib/navigation";
import {
  X, Phone, ChatCircleDots, EnvelopeSimple, DotsThree,
  Copy, CalendarBlank, PencilSimple, ArrowRight,
} from "@phosphor-icons/react";
import GlassCard from "@/components/glass/GlassCard";
import StatusPill from "@/components/glass/StatusPill";
import { api, formatPhone, STATUS_LABELS, normalizeApiError } from "@/lib/api";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import {
  avatarGradient, formatPatientId, formatRupee, formatSource,
  getInitials, isVipPatient, relativeTime,
} from "./patientUtils";
import { useAuth } from "@/lib/auth";
import {
  formatHospitalDate,
  formatHospitalTime12h,
  resolveHospitalTimezone,
} from "@/lib/tenantTime";

const copyText = async (text, label) => {
  try {
    await navigator.clipboard.writeText(text);
    toast.success(`${label} copied`);
  } catch {
    toast.error("Could not copy");
  }
};

const QuickAction = ({ icon: Icon, label, href, onClick, accent }) => (
  <button
    type="button"
    onClick={onClick}
    className="flex flex-col items-center gap-1.5 group"
  >
    <span className={cn(
      "w-11 h-11 rounded-full flex items-center justify-center border border-white/60 bg-white/50 hover:bg-white/80 transition-colors shadow-sm",
      accent,
    )}>
      <Icon weight={label === "Message" ? "fill" : "regular"} className="w-5 h-5 text-[#4338CA]" />
    </span>
    <span className="text-[11px] text-text-secondary group-hover:text-[#022C22]">{label}</span>
  </button>
);

const OverviewRow = ({ label, value, children }) => (
  <div className="flex items-start justify-between gap-3 py-2 border-b border-white/40 last:border-0">
    <span className="text-[12px] text-text-muted shrink-0">{label}</span>
    <div className="text-[12px] text-[#022C22] text-right font-medium">{children || value || "—"}</div>
  </div>
);

const PatientPreviewPanel = ({ patient, onClose, onEdit, className }) => {
  const { tenant } = useAuth();
  const hospitalTz = resolveHospitalTimezone(tenant);
  const [detail, setDetail] = useState(null);
  const [appointments, setAppointments] = useState([]);
  const [activity, setActivity] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!patient?.id) return;
    const controller = new AbortController();
    setLoading(true);

    const load = async () => {
      try {
        const [detailRes, holisticRes] = await Promise.all([
          api.get(`/patients/${patient.id}`, { signal: controller.signal }),
          api.get(`/patients/${patient.id}/holistic-view`, { signal: controller.signal }).catch(() => null),
        ]);

        if (controller.signal.aborted) return;
        const p = detailRes.data;
        setDetail(p);

        const appts = holisticRes?.data?.appointments || [];
        setAppointments(appts);

        const items = [];
        if (p.created_at) {
          items.push({
            id: "created",
            title: `${formatSource(p.inquiry_source)} inquiry received`,
            time: p.created_at,
          });
        }
        if (p.last_contact_at) {
          items.push({
            id: "contact",
            title: `Last contact via ${formatSource(p.inquiry_source)}`,
            time: p.last_contact_at,
          });
        }
        if (p.referral_doctor) {
          items.push({
            id: "assigned",
            title: `Assigned to ${p.referral_doctor}`,
            time: p.updated_at || p.created_at,
          });
        }

        try {
          const tl = await api.get(`/patients/${patient.id}/timeline`, { signal: controller.signal });
          if (!controller.signal.aborted && tl.data?.length) {
            tl.data.slice(0, 5).forEach((entry, i) => {
              items.push({
                id: `tl-${i}`,
                title: entry.title || entry.summary || "Activity",
                time: entry.occurred_at,
              });
            });
          }
        } catch { /* timeline optional */ }

        items.sort((a, b) => new Date(b.time) - new Date(a.time));
        setActivity(items.slice(0, 5));
      } catch (e) {
        if (e?.code !== "ERR_CANCELED") {
          toast.error(normalizeApiError(e, "Could not load patient details"));
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    };

    load();
    return () => controller.abort();
  }, [patient?.id]);

  const p = detail || patient;
  const nextAppt = useMemo(() => {
    const now = new Date();
    return (appointments || [])
      .filter((a) => a.scheduled_at && new Date(a.scheduled_at) >= now && !["cancelled", "completed"].includes(a.status))
      .sort((a, b) => new Date(a.scheduled_at) - new Date(b.scheduled_at))[0];
  }, [appointments]);

  return (
    <aside
      data-testid="patient-preview-panel"
      className={cn(
        "flex flex-col min-h-0 h-full rounded-2xl glass-card border border-white/60 overflow-hidden",
        className,
      )}
    >
      <div className="shrink-0 flex items-center justify-end px-4 pt-4">
        <button
          type="button"
          onClick={onClose}
          className="p-2 rounded-full hover:bg-white/60 text-text-muted hover:text-[#022C22] transition-colors"
          aria-label="Close preview"
        >
          <X weight="bold" className="w-4 h-4" />
        </button>
      </div>

      <div className="flex-1 overflow-y-auto scrollbar-thin px-5 pb-5 space-y-5">
        {/* Profile header */}
        <div className="text-center">
          <div className={cn(
            "w-20 h-20 mx-auto rounded-full bg-gradient-to-br flex items-center justify-center text-white font-heading text-2xl font-semibold shadow-md",
            avatarGradient(p.name),
          )}>
            {getInitials(p.name)}
          </div>
          <h2 className="font-heading text-lg font-semibold text-[#022C22] mt-3">{p.name}</h2>
          {isVipPatient(p) && (
            <span className="status-pill mt-1.5 font-semibold uppercase tracking-wide bg-violet-100 text-violet-700 border-violet-200">
              VIP Patient
            </span>
          )}
          <p className="text-[12px] text-text-muted font-mono mt-1">{formatPatientId(p)}</p>
        </div>

        {/* Quick actions */}
        <div className="flex items-center justify-center gap-5">
          <QuickAction
            icon={Phone}
            label="Call"
            onClick={() => p.phone && (window.location.href = `tel:${p.phone}`)}
          />
          <QuickAction
            icon={ChatCircleDots}
            label="Message"
            accent="text-[#25D366]"
            onClick={() => window.open(`/inbox`, "_self")}
          />
          <QuickAction
            icon={EnvelopeSimple}
            label="Email"
            onClick={() => p.email && (window.location.href = `mailto:${p.email}`)}
          />
          <QuickAction
            icon={DotsThree}
            label="More"
            onClick={() => onEdit?.(p)}
          />
        </div>

        {/* Overview */}
        <GlassCard padding={false} className="px-4 py-1">
          <div className="py-2 border-b border-white/40 mb-1">
            <span className="text-[11px] uppercase tracking-[0.08em] font-semibold text-text-muted">Overview</span>
          </div>
          {loading ? (
            <div className="py-6 text-center text-[12px] text-text-muted animate-pulse">Loading…</div>
          ) : (
            <>
              <OverviewRow label="Status">
                <StatusPill status={p.status} label={STATUS_LABELS[p.status]} />
              </OverviewRow>
              <OverviewRow label="Source" value={formatSource(p.inquiry_source)} />
              <OverviewRow label="Assigned To" value={p.referral_doctor || "—"} />
              <OverviewRow
                label="First Inquiry"
                value={p.created_at ? new Date(p.created_at).toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" }) : "—"}
              />
              <OverviewRow label="Last Activity" value={relativeTime(p.last_contact_at || p.updated_at)} />
              <OverviewRow label="Total Spent" value={formatRupee(0)} />
            </>
          )}
        </GlassCard>

        {/* Contact */}
        <GlassCard padding={false} className="px-4 py-3">
          <div className="text-[11px] uppercase tracking-[0.08em] font-semibold text-text-muted mb-3">Contact Details</div>
          <div className="space-y-3">
            <div className="flex items-center justify-between gap-2">
              <div>
                <div className="text-[10px] text-text-muted mb-0.5">Phone</div>
                <div className="text-[13px] font-mono text-[#022C22]">{formatPhone(p.phone)}</div>
              </div>
              {p.phone && (
                <button type="button" onClick={() => copyText(p.phone, "Phone")} className="p-1.5 rounded-lg hover:bg-white/60 text-text-muted" aria-label="Copy phone number">
                  <Copy className="w-4 h-4" />
                </button>
              )}
            </div>
            <div className="flex items-center justify-between gap-2">
              <div className="min-w-0">
                <div className="text-[10px] text-text-muted mb-0.5">Email</div>
                <div className="text-[13px] text-[#022C22] truncate">{p.email || "—"}</div>
              </div>
              {p.email && (
                <button type="button" onClick={() => copyText(p.email, "Email")} className="p-1.5 rounded-lg hover:bg-white/60 text-text-muted shrink-0" aria-label="Copy email address">
                  <Copy className="w-4 h-4" />
                </button>
              )}
            </div>
          </div>
        </GlassCard>

        {/* Next appointment */}
        {nextAppt && (
          <GlassCard padding={false} className="px-4 py-3 border-l-4 border-l-[#6366F1]">
            <div className="text-[11px] uppercase tracking-[0.08em] font-semibold text-text-muted mb-2">Next Appointment</div>
            <div className="flex items-start gap-3">
              <div className="w-9 h-9 rounded-xl bg-[#6366F1]/10 flex items-center justify-center shrink-0">
                <CalendarBlank weight="fill" className="w-4 h-4 text-[#6366F1]" />
              </div>
              <div className="min-w-0">
                <div className="text-[13px] font-semibold text-[#022C22]">
                  {formatHospitalDate(nextAppt.scheduled_at, hospitalTz, "MMM d, yyyy")}
                  {" · "}
                  {formatHospitalTime12h(nextAppt.scheduled_at, hospitalTz)}
                </div>
                <div className="text-[12px] text-text-secondary mt-0.5">
                  {nextAppt.notes || nextAppt.chief_complaint || "Consultation"}
                </div>
                {nextAppt.doctor_name && (
                  <div className="flex items-center gap-2 mt-2">
                    <div className={cn("w-6 h-6 rounded-full bg-gradient-to-br flex items-center justify-center text-[10px] text-white font-semibold", avatarGradient(nextAppt.doctor_name))}>
                      {getInitials(nextAppt.doctor_name)}
                    </div>
                    <span className="text-[12px] text-[#022C22]">{nextAppt.doctor_name}</span>
                  </div>
                )}
              </div>
            </div>
          </GlassCard>
        )}

        {/* Recent activity */}
        <GlassCard padding={false} className="px-4 py-3">
          <div className="flex items-center justify-between mb-3">
            <span className="text-[11px] uppercase tracking-[0.08em] font-semibold text-text-muted">Recent Activity</span>
            <Link to={`/patients/${p.id}`} className="text-[11px] text-[#6366F1] hover:underline font-medium">
              View All
            </Link>
          </div>
          <div className="space-y-3">
            {activity.length === 0 && (
              <p className="text-[12px] text-text-muted py-2">No recent activity</p>
            )}
            {activity.map((item) => (
              <div key={item.id} className="flex gap-3">
                <div className="w-2 h-2 rounded-full bg-[#6366F1] mt-1.5 shrink-0" />
                <div className="min-w-0">
                  <div className="text-[12px] text-[#022C22] leading-snug">{item.title}</div>
                  <div className="text-[11px] text-text-muted mt-0.5">{relativeTime(item.time)}</div>
                </div>
              </div>
            ))}
          </div>
        </GlassCard>

        {/* Footer actions */}
        <div className="flex gap-2 pt-1">
          <Link
            to={`/patients/${p.id}`}
            className="flex-1 flex items-center justify-center gap-2 text-[13px] px-4 py-2.5 rounded-xl btn-primary"
          >
            Full Profile
            <ArrowRight className="w-4 h-4" />
          </Link>
          <button
            type="button"
            data-testid="patient-preview-edit"
            onClick={() => onEdit?.(p)}
            className="flex items-center justify-center gap-1.5 px-4 py-2.5 rounded-xl bg-white/60 hover:bg-white/80 border border-white/60 text-[13px] text-[#022C22] transition-colors"
          >
            <PencilSimple className="w-4 h-4" />
            Edit
          </button>
        </div>
      </div>
    </aside>
  );
};

export default PatientPreviewPanel;

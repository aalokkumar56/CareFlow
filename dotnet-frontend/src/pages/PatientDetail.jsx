import React, { useCallback, useEffect, useState } from "react";
import { useParams, Link, useSearchParams } from "react-router-dom";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";
import { fetchTemplates } from "@/lib/templatesCache";
import AppShell from "@/components/layout/AppShell";
import WhatsAppChatPanel from "@/components/WhatsAppChatPanel";
import {
  api, formatPhone, formatBloodGroup, formatGender, formatPatientDemographics, normalizeApiError,
  STATUS_LABELS, STATUS_COLORS, NOTE_TYPE_LABELS,
  openAuthorizedHtml, downloadAuthorizedFile,
} from "@/lib/api";
import {
  ChatCircleDots, CalendarBlank, ArrowLeft, ArrowRight,
  ClockCounterClockwise, Sparkle, Note, Warning, Heartbeat,
  User, PencilSimple, Sun, Stethoscope, Pill, Syringe, Flask,
} from "@phosphor-icons/react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import {
  Breadcrumb, BreadcrumbList, BreadcrumbItem, BreadcrumbLink, BreadcrumbPage, BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import {
  Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription,
} from "@/components/ui/sheet";
import { toast } from "sonner";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import DepartmentSelect from "@/components/forms/DepartmentSelect";
import useDepartments from "@/hooks/useDepartments";
import StatusPill from "@/components/glass/StatusPill";

import Allergies from "./ehr/Allergies";
import Prescriptions from "./ehr/Prescriptions";
import Vitals from "./ehr/Vitals";
import { ClinicalNotes, MedicalHistory, FamilyHistory } from "./ehr/ClinicalNotes";
import Lifestyle from "./ehr/Lifestyle";

const isSameDay = (dateStr) => {
  if (!dateStr) return false;
  const d = new Date(dateStr);
  const now = new Date();
  return (
    d.getFullYear() === now.getFullYear() &&
    d.getMonth() === now.getMonth() &&
    d.getDate() === now.getDate()
  );
};

const PATIENT_TAB_TRIGGER =
  "rounded-lg data-[state=active]:bg-[#064E3B] data-[state=active]:text-white";

const PatientBreadcrumb = ({ name }) => (
  <Breadcrumb>
    <BreadcrumbList className="text-[13px] text-text-secondary">
      <BreadcrumbItem>
        <BreadcrumbLink asChild className="text-[#064E3B] hover:text-[#022C22] font-medium">
          <Link to="/patients">Patients</Link>
        </BreadcrumbLink>
      </BreadcrumbItem>
      <BreadcrumbSeparator className="text-text-muted" />
      <BreadcrumbItem>
        <BreadcrumbPage className="font-heading font-semibold text-[#022C22] truncate max-w-[min(100vw-12rem,28rem)]">
          {name}
        </BreadcrumbPage>
      </BreadcrumbItem>
    </BreadcrumbList>
  </Breadcrumb>
);

const PatientDetail = () => {
  const { id } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const { canFetch } = usePermissions();
  const canClinical = canFetch(PERMISSIONS.ClinicalView);
  const canWhatsApp = canFetch(PERMISSIONS.WhatsAppView) || canFetch(PERMISSIONS.WhatsAppSend);
  const [data, setData] = useState(null);
  const [allergies, setAllergies] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [editForm, setEditForm] = useState({});
  const [activeTab, setActiveTab] = useState("today");
  const [overlayPanel, setOverlayPanel] = useState(null);

  const applyPatientForm = useCallback((p) => {
    const gender = p.gender && String(p.gender).toLowerCase() !== "unknown" ? String(p.gender).toLowerCase() : "";
    const bloodGroup = p.blood_group && String(p.blood_group).toLowerCase() !== "unknown"
      ? String(p.blood_group).toLowerCase()
      : "";
    setEditForm({
        email: p.email || "",
        email_notifications_enabled: p.email_notifications_enabled || false,
        status: p.status,
        department: p.department || "",
        tags: (p.tags || []).join(", "),
        notes: p.notes || "",
        follow_up_date: p.follow_up_date?.slice(0, 10) || "",
        date_of_birth: p.date_of_birth?.slice(0, 10) || "",
        age: p.age ?? "",
        gender,
        blood_group: bloodGroup,
        occupation: p.occupation || "",
        marital_status: p.marital_status || "",
        gov_id_type: p.gov_id_type || "",
        gov_id_number: p.gov_id_number || "",
        address_line1: p.address_line1 || "",
        address_line2: p.address_line2 || "",
        city: p.city || "",
        state: p.state || "",
        pincode: p.pincode || "",
        emergency_contact_name: p.emergency_contact_name || "",
        emergency_contact_phone: p.emergency_contact_phone || "",
        emergency_contact_relation: p.emergency_contact_relation || "",
        ai_lead_score: p.ai_lead_score || 0,
      });
  }, []);

  const loadHolistic = useCallback(() => {
    if (!canClinical) return Promise.resolve();
    return api.get(`/patients/${id}/holistic-view`).then((r) => {
      setData(r.data);
      setAllergies(r.data.allergies || []);
      applyPatientForm(r.data.patient || r.data);
    });
  }, [id, canClinical, applyPatientForm]);

  const loadBasic = useCallback(() => {
    return api.get(`/patients/${id}`).then((r) => {
      setData({ patient: r.data });
      applyPatientForm(r.data);
    });
  }, [id, applyPatientForm]);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;

    const load = async () => {
      try {
        if (canClinical) {
          await api.get(`/patients/${id}/holistic-view`, { signal }).then((r) => {
            if (signal.aborted) return;
            setData(r.data);
            setAllergies(r.data.allergies || []);
            applyPatientForm(r.data.patient || r.data);
          });
        } else {
          await api.get(`/patients/${id}`, { signal }).then((r) => {
            if (signal.aborted) return;
            setData({ patient: r.data });
            applyPatientForm(r.data);
          });
        }
        if (canWhatsApp) {
          const tpl = await fetchTemplates({ signal });
          if (!signal.aborted) setTemplates(tpl);
        }
      } catch (e) {
        if (e?.code === "ERR_CANCELED" || signal.aborted) return;
      }
    };

    load();
    return () => controller.abort();
  }, [id, canClinical, canWhatsApp, applyPatientForm]);

  const refreshHolistic = () => {
    if (canClinical) loadHolistic();
    else loadBasic();
  };

  const handleTabChange = (tab) => {
    setOverlayPanel(null);
    setActiveTab(tab);
  };

  const openDetailsForEdit = (patient) => {
    applyPatientForm(patient);
    handleTabChange("details");
  };

  useEffect(() => {
    if (searchParams.get("edit") === "1" && data?.patient) {
      openDetailsForEdit(data.patient);
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.delete("edit");
        return next;
      }, { replace: true });
    }
  }, [searchParams, setSearchParams, data]);

  const save = async () => {
    try {
      const tagsArray = editForm.tags
        ? editForm.tags.split(",").map((t) => t.trim()).filter(Boolean)
        : [];

      await api.patch(`/patients/${id}`, {
        email: editForm.email || null,
        email_notifications_enabled: editForm.email_notifications_enabled || false,
        status: editForm.status,
        department: editForm.department || null,
        notes: editForm.notes,
        tags: tagsArray,
        follow_up_date: editForm.follow_up_date || null,
        date_of_birth: editForm.date_of_birth || null,
        age: editForm.age ? parseInt(editForm.age) : null,
        gender: editForm.gender || null,
        blood_group: editForm.blood_group || null,
        occupation: editForm.occupation || null,
        marital_status: editForm.marital_status || null,
        gov_id_type: editForm.gov_id_type || null,
        gov_id_number: editForm.gov_id_number || null,
        address_line1: editForm.address_line1 || null,
        address_line2: editForm.address_line2 || null,
        city: editForm.city || null,
        state: editForm.state || null,
        pincode: editForm.pincode || null,
        emergency_contact_name: editForm.emergency_contact_name || null,
        emergency_contact_phone: editForm.emergency_contact_phone || null,
        emergency_contact_relation: editForm.emergency_contact_relation || null,
      });
      toast.success("Patient updated");
      refreshHolistic();
      setOverlayPanel(null);
    } catch (e) {
      toast.error(normalizeApiError(e, "Update failed"));
    }
  };

  if (!data) {
    return (
      <AppShell breadcrumb={<PatientBreadcrumb name="Loading…" />} showDate={false} hideHeaderSearch hideHospitalBadge>
        {null}
      </AppShell>
    );
  }

  const p = data.patient;
  const severeAllergies = allergies.filter(
    (a) => a.severity === "severe" || a.severity === "life_threatening"
  );

  return (
    <AppShell breadcrumb={<PatientBreadcrumb name={p.name} />} showDate={false} hideHeaderSearch hideHospitalBadge>
      <PageContent>
        <Link
          to="/patients"
          data-testid="patient-back-link"
          className="inline-flex items-center gap-1.5 text-[13px] text-text-secondary hover:text-[#064E3B] w-fit"
        >
          <ArrowLeft weight="regular" className="w-4 h-4" /> Back to list
        </Link>

        {/* Compact patient header */}
        <GlassCard>
          <div className="flex flex-wrap items-start gap-4 justify-between">
            <div className="flex items-center gap-3 min-w-0">
              <div className="w-12 h-12 rounded-sm bg-[#064E3B] text-white flex items-center justify-center font-heading text-lg font-semibold shrink-0">
                {p.name?.[0]?.toUpperCase()}
              </div>
              <div className="min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <h2
                    className="font-heading text-lg font-semibold text-[#022C22]"
                    data-testid="patient-name"
                  >
                    {p.name}
                  </h2>
                  <button
                    type="button"
                    data-testid="patient-edit-icon"
                    onClick={() => openDetailsForEdit(p)}
                    className="p-1.5 rounded-sm text-[#064E3B] hover:bg-primary-soft border border-transparent hover:border-[#064E3B]/20 transition-colors"
                    title="Edit patient"
                    aria-label="Edit patient"
                  >
                    <PencilSimple weight="regular" className="w-4 h-4" />
                  </button>
                  {canWhatsApp && (
                    <button
                      type="button"
                      data-testid="patient-whatsapp-icon"
                      onClick={() => setOverlayPanel(overlayPanel === "whatsapp" ? null : "whatsapp")}
                      className="p-1.5 rounded-sm text-[#064E3B] hover:bg-primary-soft border border-transparent hover:border-[#064E3B]/20 transition-colors"
                      title="WhatsApp chat"
                      aria-label="WhatsApp chat"
                    >
                      <ChatCircleDots weight="fill" className="w-4 h-4" />
                    </button>
                  )}
                </div>
                <div className="text-[12px] text-text-secondary mt-0.5" data-testid="patient-demographics">
                  {formatPatientDemographics(p)}
                </div>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <StatusPill status={p.status} label={STATUS_LABELS[p.status]} />
              {p.follow_up_date && (
                <div className="text-[12px] text-text-secondary">
                  Follow-up:{" "}
                  <span className="font-medium text-[#022C22]">
                    {new Date(p.follow_up_date).toLocaleDateString()}
                  </span>
                </div>
              )}
            </div>
          </div>

          {severeAllergies.length > 0 && (
            <div className="mt-4 border border-red-300 bg-red-50 rounded-sm p-3" data-testid="severe-allergy-banner">
              <div className="flex items-center gap-1.5 mb-1">
                <Warning weight="fill" className="w-3.5 h-3.5 text-red-700" />
                <span className="text-[10px] uppercase tracking-wider font-semibold text-red-800">
                  Severe allergy alert
                </span>
              </div>
              <div className="text-[12px] text-red-900">
                {severeAllergies.map((a) => a.allergen).join(", ")}
              </div>
            </div>
          )}
        </GlassCard>

        {/* Main tabs */}
        <Tabs value={activeTab} onValueChange={handleTabChange} className="w-full">
          <TabsList
            className="glass-card h-auto p-1 rounded-xl bg-white/60 border border-white/50 flex flex-wrap gap-1 w-full justify-start overflow-x-auto"
            data-testid="patient-tabs"
          >
            <TabsTrigger value="today" className={PATIENT_TAB_TRIGGER} data-testid="tab-today">
              <Sun weight="regular" className="w-3.5 h-3.5 mr-1" /> Today
            </TabsTrigger>
            <TabsTrigger value="details" className={PATIENT_TAB_TRIGGER} data-testid="tab-details">
              <User weight="regular" className="w-3.5 h-3.5 mr-1" /> Details
            </TabsTrigger>
            {canClinical && (
              <>
                <TabsTrigger value="allergies" className={PATIENT_TAB_TRIGGER} data-testid="tab-allergies">
                  Allergies
                </TabsTrigger>
                <TabsTrigger value="prescriptions" className={PATIENT_TAB_TRIGGER} data-testid="tab-prescriptions">
                  Prescriptions
                </TabsTrigger>
                <TabsTrigger value="vitals" className={PATIENT_TAB_TRIGGER} data-testid="tab-vitals">
                  Vitals
                </TabsTrigger>
                <TabsTrigger value="notes" className={PATIENT_TAB_TRIGGER} data-testid="tab-notes">
                  Notes
                </TabsTrigger>
                <TabsTrigger value="history" className={PATIENT_TAB_TRIGGER} data-testid="tab-history">
                  History
                </TabsTrigger>
                <TabsTrigger value="lifestyle" className={PATIENT_TAB_TRIGGER} data-testid="tab-lifestyle">
                  Lifestyle
                </TabsTrigger>
              </>
            )}
            <TabsTrigger value="timeline" className={PATIENT_TAB_TRIGGER} data-testid="tab-timeline">
              Timeline
            </TabsTrigger>
          </TabsList>

          <TabsContent value="today" className="mt-3">
            <TodayOverview
              data={data}
              allergies={allergies}
              onNavigate={handleTabChange}
              onOpenEdit={() => openDetailsForEdit(p)}
            />
          </TabsContent>

          <TabsContent value="details" className="mt-3">
            <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl p-6" data-testid="patient-details-panel">
              <EditPatientForm editForm={editForm} setEditForm={setEditForm} onSave={save} />
            </div>
          </TabsContent>

          <TabsContent value="allergies" className="mt-3">
            {activeTab === "allergies" && canClinical && (
              <Allergies
                patientId={id}
                items={allergies}
                onItemsChange={setAllergies}
                skipInitialFetch
                onDataChanged={refreshHolistic}
              />
            )}
          </TabsContent>
          <TabsContent value="prescriptions" className="mt-3">
            {activeTab === "prescriptions" && canClinical && (
              <Prescriptions patientId={id} doctorName={p.referral_doctor} onDataChanged={refreshHolistic} />
            )}
          </TabsContent>
          <TabsContent value="vitals" className="mt-3">
            {activeTab === "vitals" && canClinical && (
              <Vitals patientId={id} onDataChanged={refreshHolistic} />
            )}
          </TabsContent>
          <TabsContent value="notes" className="mt-3">
            {activeTab === "notes" && canClinical && (
              <ClinicalNotes patientId={id} onDataChanged={refreshHolistic} />
            )}
          </TabsContent>
          <TabsContent value="history" className="mt-3 space-y-4">
            {activeTab === "history" && canClinical && (
              <>
                <MedicalHistory patientId={id} />
                <FamilyHistory patientId={id} />
              </>
            )}
          </TabsContent>
          <TabsContent value="lifestyle" className="mt-3">
            {activeTab === "lifestyle" && canClinical && (
              <Lifestyle patientId={id} />
            )}
          </TabsContent>
          <TabsContent value="timeline" className="mt-3">
            {activeTab === "timeline" && (
              <MedicalTimeline patientId={id} data={data} enabled={activeTab === "timeline"} />
            )}
          </TabsContent>
        </Tabs>

        {canWhatsApp && overlayPanel === "whatsapp" && (
          <Sheet open onOpenChange={(v) => !v && setOverlayPanel(null)}>
            <SheetContent className="w-full sm:max-w-lg p-0 flex flex-col">
              <SheetHeader className="px-4 pt-4 pb-0 text-left">
                <SheetTitle className="font-heading">WhatsApp</SheetTitle>
                <SheetDescription>Chat with {p.name}</SheetDescription>
              </SheetHeader>
              <div className="flex-1 min-h-0 p-4 pt-2">
                <WhatsAppChatPanel
                  patientId={id}
                  patientName={p.name}
                  templates={templates}
                  className="h-full min-h-[480px]"
                />
              </div>
            </SheetContent>
          </Sheet>
        )}
      </PageContent>
    </AppShell>
  );
};

const SectionLink = ({ label, tab, onNavigate, onClick }) => (
  <button
    type="button"
    onClick={() => (onClick ? onClick() : onNavigate(tab))}
    className="text-[12px] text-[#064E3B] hover:text-[#022C22] font-medium flex items-center gap-1 shrink-0"
  >
    {label} <ArrowRight weight="bold" className="w-3 h-3" />
  </button>
);

const EmptySection = ({ message }) => (
  <div className="text-[13px] text-text-muted py-4 text-center">{message}</div>
);

const TodayOverview = ({ data, allergies, onNavigate, onOpenEdit }) => {
  const p = data.patient;
  const today = new Date().toLocaleDateString(undefined, {
    weekday: "long", year: "numeric", month: "long", day: "numeric",
  });

  const appointments = data.appointments || [];
  const todayAppointments = appointments.filter((a) => isSameDay(a.scheduled_at));
  const todayVitals = (data.vital_signs || []).filter(
    (v) => isSameDay(v.measured_at)
  );
  const todayNotes = (data.clinical_notes || []).filter(
    (n) => isSameDay(n.created_at)
  );
  const todayPrescriptions = (data.recent_prescriptions || []).filter(
    (rx) => isSameDay(rx.prescribed_at)
  );

  const startOfTomorrow = new Date();
  startOfTomorrow.setHours(24, 0, 0, 0);
  const endRange = new Date();
  endRange.setDate(endRange.getDate() + 7);
  endRange.setHours(23, 59, 59, 999);

  const upcomingAppointments = appointments
    .filter((a) => {
      const d = new Date(a.scheduled_at);
      return d >= startOfTomorrow && d <= endRange && !["cancelled", "completed"].includes(a.status);
    })
    .sort((a, b) => new Date(a.scheduled_at) - new Date(b.scheduled_at))
    .slice(0, 5);

  return (
    <div className="space-y-4" data-testid="today-overview">
      <div className="text-[12px] text-text-secondary">
        Clinical overview for <span className="font-medium text-[#022C22]">{today}</span>
      </div>

      {/* Today's visit */}
      <OverviewCard
        title="Today's Visit"
        icon={<CalendarBlank weight="fill" className="w-4 h-4 text-[#064E3B]" />}
        link={<SectionLink label="All appointments" tab="timeline" onNavigate={onNavigate} />}
      >
        {todayAppointments.length === 0 ? (
          <EmptySection message="No appointment scheduled for today." />
        ) : (
          <div className="space-y-3">
            {todayAppointments.map((a) => (
              <AppointmentRow key={a.id} a={a} />
            ))}
          </div>
        )}
      </OverviewCard>

      {upcomingAppointments.length > 0 && (
        <OverviewCard
          title="Upcoming Appointments"
          icon={<CalendarBlank weight="regular" className="w-4 h-4 text-[#064E3B]" />}
          link={<SectionLink label="View all" tab="timeline" onNavigate={onNavigate} />}
        >
          <div className="space-y-2">
            {upcomingAppointments.map((a) => (
              <div key={a.id} className="flex flex-wrap items-center justify-between gap-2 text-[12px] py-1.5 border-b border-subtle last:border-0">
                <span className="font-medium text-[#022C22]">
                  {new Date(a.scheduled_at).toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" })}
                  {" · "}{new Date(a.scheduled_at).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                </span>
                <span className="text-text-secondary">{a.doctor_name} · {a.department}</span>
              </div>
            ))}
          </div>
        </OverviewCard>
      )}

      {/* AI summary */}
      {p.ai_summary && (
        <div className="border border-[#064E3B]/15 bg-primary-soft rounded-sm p-4">
          <div className="flex items-center justify-between gap-2 mb-1">
            <div className="flex items-center gap-2">
              <Sparkle weight="fill" className="w-3.5 h-3.5 text-[#064E3B]" />
              <span className="text-[10px] uppercase tracking-wider font-semibold text-[#064E3B]">
                AI Summary
              </span>
            </div>
          </div>
          <div className="text-[13px] text-[#022C22]">{p.ai_summary}</div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Today's vitals */}
        <OverviewCard
          title="Today's Vitals"
          icon={<Heartbeat weight="fill" className="w-4 h-4 text-red-500" />}
          link={<SectionLink label="Full vitals history" tab="vitals" onNavigate={onNavigate} />}
        >
          {todayVitals.length === 0 ? (
            <EmptySection message="No vitals recorded today." />
          ) : (
            <div className="space-y-3">
              {todayVitals.map((v) => (
                <div key={v.id}>
                  <div className="text-[11px] text-text-muted mb-1">
                    {new Date(v.measured_at).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                    {v.recorded_by_name ? ` · ${v.recorded_by_name}` : ""}
                  </div>
                  <div className="flex flex-wrap gap-x-4 gap-y-1 text-[12px]">
                    {(v.systolic_bp || v.diastolic_bp) && (
                      <VitalStat label="BP" val={`${v.systolic_bp || "—"}/${v.diastolic_bp || "—"}`} />
                    )}
                    {v.heart_rate && <VitalStat label="HR" val={`${v.heart_rate} bpm`} />}
                    {v.temperature && <VitalStat label="Temp" val={`${v.temperature}°F`} />}
                    {v.oxygen_saturation && <VitalStat label="SpO₂" val={`${v.oxygen_saturation}%`} />}
                    {v.weight_kg && <VitalStat label="Wt" val={`${v.weight_kg} kg`} />}
                    {v.height_cm && <VitalStat label="Ht" val={`${v.height_cm} cm`} />}
                    {v.blood_sugar_fasting && <VitalStat label="FBS" val={v.blood_sugar_fasting} />}
                  </div>
                </div>
              ))}
            </div>
          )}
        </OverviewCard>

        {/* Today's notes */}
        <OverviewCard
          title="Today's Clinical Notes"
          icon={<Note weight="fill" className="w-4 h-4 text-[#064E3B]" />}
          link={<SectionLink label="All notes" tab="notes" onNavigate={onNavigate} />}
        >
          {todayNotes.length === 0 ? (
            <EmptySection message="No clinical notes recorded today." />
          ) : (
            <div className="space-y-3">
              {todayNotes.map((n) => (
                <div key={n.id} className="border-b border-subtle last:border-0 pb-3 last:pb-0">
                  <div className="text-[12px] font-semibold text-[#022C22]">
                    {NOTE_TYPE_LABELS[n.note_type] || n.note_type}
                    <span className="text-text-muted font-normal ml-2">
                      {new Date(n.created_at).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                    </span>
                  </div>
                  {n.subjective && (
                    <div className="text-[12px] text-[#022C22] mt-1 line-clamp-2">
                      <span className="text-text-muted text-[10px] uppercase tracking-wider mr-1">S</span>
                      {n.subjective}
                    </div>
                  )}
                  {n.assessment && (
                    <div className="text-[12px] text-text-secondary mt-0.5 line-clamp-1">
                      <span className="text-text-muted text-[10px] uppercase tracking-wider mr-1">A</span>
                      {n.assessment}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </OverviewCard>
      </div>

      {/* Active allergies + today's prescriptions */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <OverviewCard
          title="Active Allergies"
          icon={<Warning weight="fill" className="w-4 h-4 text-amber-600" />}
          link={<SectionLink label="Manage allergies" tab="allergies" onNavigate={onNavigate} />}
        >
          {(allergies.length === 0 && (data.allergies || []).length === 0) ? (
            <EmptySection message="No allergies on record." />
          ) : (
            <div className="flex flex-wrap gap-2">
              {(allergies.length ? allergies : data.allergies || []).map((a) => (
                <span
                  key={a.id}
                  className={`text-[11px] px-2 py-1 rounded-sm border ${
                    a.severity === "severe" || a.severity === "life_threatening"
                      ? "bg-red-50 text-red-800 border-red-200"
                      : "bg-secondary text-text-secondary border-subtle"
                  }`}
                >
                  {a.allergen}
                  {a.severity ? ` (${a.severity.replace(/_/g, " ")})` : ""}
                </span>
              ))}
            </div>
          )}
        </OverviewCard>

        <OverviewCard
          title="Today's Prescriptions"
          icon={<Note weight="regular" className="w-4 h-4 text-[#064E3B]" />}
          link={<SectionLink label="All prescriptions" tab="prescriptions" onNavigate={onNavigate} />}
        >
          {todayPrescriptions.length === 0 ? (
            <EmptySection message="No prescriptions issued today." />
          ) : (
            <div className="space-y-2">
              {todayPrescriptions.map((rx) => (
                <div key={rx.id} className="text-[12px] text-[#022C22]">
                  <span className="font-medium">{rx.doctor_name || "—"}</span>
                  {rx.diagnosis && <span className="text-text-secondary"> · {rx.diagnosis}</span>}
                </div>
              ))}
            </div>
          )}
        </OverviewCard>
      </div>

      {p.notes && (
        <OverviewCard
          title="Lead / CRM Notes"
          icon={<Note weight="regular" className="w-4 h-4 text-text-muted" />}
          link={<SectionLink label="Edit notes" onClick={onOpenEdit} />}
        >
          <div className="text-[13px] text-[#022C22] whitespace-pre-wrap">{p.notes}</div>
        </OverviewCard>
      )}
    </div>
  );
};

const AppointmentRow = ({ a }) => (
  <div className="border border-subtle rounded-sm p-3 bg-secondary/20">
    <div className="flex flex-wrap items-start justify-between gap-2">
      <div>
        <div className="text-[13px] font-semibold text-[#022C22]">
          {new Date(a.scheduled_at).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
          {" · "}{a.doctor_name || "—"}
        </div>
        <div className="text-[12px] text-text-secondary mt-0.5">
          {a.department || "—"} ·{" "}
          <span className="capitalize">{a.status?.replace(/_/g, " ")}</span>
          {a.duration_minutes ? ` · ${a.duration_minutes} min` : ""}
        </div>
      </div>
      {a.checked_in_at && (
        <span className="text-[10px] uppercase tracking-wider font-semibold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-sm border border-emerald-200">
          Checked in
        </span>
      )}
    </div>
    {a.chief_complaint && (
      <div className="mt-2 text-[12px]">
        <span className="text-text-muted font-semibold uppercase text-[10px] tracking-wider">Chief complaint</span>
        <div className="text-[#022C22] mt-0.5">{a.chief_complaint}</div>
      </div>
    )}
    {a.notes && (
      <div className="mt-2 text-[12px] text-text-secondary">{a.notes}</div>
    )}
  </div>
);

const OverviewCard = ({ title, icon, link, children }) => (
  <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl">
    <div className="px-5 py-3 border-b border-white/60 flex items-center justify-between gap-2">
      <div className="flex items-center gap-2">
        {icon}
        <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">{title}</h3>
      </div>
      {link}
    </div>
    <div className="p-5">{children}</div>
  </div>
);

const VitalStat = ({ label, val }) => (
  <span className="text-[#022C22]">
    <span className="text-text-muted text-[10px] uppercase tracking-wider mr-1">{label}</span>
    <span className="font-medium">{val}</span>
  </span>
);

const EditPatientForm = ({ editForm, setEditForm, onSave }) => {
  const { departments } = useDepartments();

  return (
  <div className="space-y-4" data-testid="edit-patient-form">
    <Tabs defaultValue="basic" className="w-full">
      <TabsList className="bg-gray-50 border border-subtle rounded-xl h-auto p-1 flex flex-wrap gap-1 w-full justify-start mb-4">
        <TabsTrigger value="basic" className={PATIENT_TAB_TRIGGER} data-testid="edit-tab-basic">Basic</TabsTrigger>
        <TabsTrigger value="personal" className={PATIENT_TAB_TRIGGER} data-testid="edit-tab-personal">Personal</TabsTrigger>
        <TabsTrigger value="address" className={PATIENT_TAB_TRIGGER} data-testid="edit-tab-address">Address</TabsTrigger>
        <TabsTrigger value="emergency" className={PATIENT_TAB_TRIGGER} data-testid="edit-tab-emergency">Emergency</TabsTrigger>
      </TabsList>

      <TabsContent value="basic" className="space-y-3" role="group" aria-label="Basic information">
        <FormField label="Email">
          <Input
            type="email"
            value={editForm.email || ""}
            onChange={(e) => setEditForm({ ...editForm, email: e.target.value })}
            className="rounded-sm"
            placeholder="patient@example.com"
          />
        </FormField>
        <label className="flex items-center gap-2 text-[13px]">
          <input
            type="checkbox"
            checked={editForm.email_notifications_enabled || false}
            onChange={(e) => setEditForm({ ...editForm, email_notifications_enabled: e.target.checked })}
            className="rounded border-subtle"
          />
          Send email notifications (appointments, when enabled in Integrations)
        </label>
        <FormField label="Status">
          <Select
            value={editForm.status || "new_inquiry"}
            onValueChange={(v) => setEditForm({ ...editForm, status: v })}
          >
            <SelectTrigger className="rounded-sm h-9" data-testid="pd-status">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {Object.entries(STATUS_LABELS).map(([k, v]) => (
                <SelectItem key={k} value={k}>{v}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FormField>
        <DepartmentSelect
          departments={departments}
          value={editForm.department}
          onChange={(v) => setEditForm({ ...editForm, department: v })}
          required={false}
          triggerClassName="rounded-sm h-9"
          showSettingsHint
        />
        <FormField label="Tags (comma-separated)">
          <Input
            value={editForm.tags}
            onChange={(e) => setEditForm({ ...editForm, tags: e.target.value })}
            className="rounded-sm"
            data-testid="pd-tags"
          />
        </FormField>
        <FormField label="Follow-up Date">
          <Input
            type="date"
            value={editForm.follow_up_date}
            onChange={(e) => setEditForm({ ...editForm, follow_up_date: e.target.value })}
            className="rounded-sm"
            data-testid="pd-follow-up-date"
          />
        </FormField>
        <FormField label="Notes">
          <Textarea
            value={editForm.notes}
            onChange={(e) => setEditForm({ ...editForm, notes: e.target.value })}
            className="rounded-sm"
            rows={3}
            data-testid="pd-notes"
          />
        </FormField>
      </TabsContent>

      <TabsContent value="personal" className="space-y-3" role="group" aria-label="Personal information">
        <FormField label="Date of Birth">
          <Input
            type="date"
            value={editForm.date_of_birth}
            onChange={(e) => setEditForm({ ...editForm, date_of_birth: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
        <FormField label="Age">
          <Input
            type="number"
            value={editForm.age}
            onChange={(e) => setEditForm({ ...editForm, age: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
        <FormField label="Gender">
          <Select
            value={editForm.gender || undefined}
            onValueChange={(v) => setEditForm({ ...editForm, gender: v })}
          >
            <SelectTrigger className="rounded-sm h-9" data-testid="pd-gender"><SelectValue placeholder="Select gender" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="male">Male</SelectItem>
              <SelectItem value="female">Female</SelectItem>
              <SelectItem value="other">Other</SelectItem>
            </SelectContent>
          </Select>
        </FormField>
        <FormField label="Blood Group">
          <Select
            value={editForm.blood_group || undefined}
            onValueChange={(v) => setEditForm({ ...editForm, blood_group: v })}
          >
            <SelectTrigger className="rounded-sm h-9" data-testid="pd-blood-group"><SelectValue placeholder="Select blood group" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="a_pos">A+</SelectItem>
              <SelectItem value="a_neg">A-</SelectItem>
              <SelectItem value="b_pos">B+</SelectItem>
              <SelectItem value="b_neg">B-</SelectItem>
              <SelectItem value="ab_pos">AB+</SelectItem>
              <SelectItem value="ab_neg">AB-</SelectItem>
              <SelectItem value="o_pos">O+</SelectItem>
              <SelectItem value="o_neg">O-</SelectItem>
            </SelectContent>
          </Select>
        </FormField>
        <FormField label="Occupation">
          <Input
            value={editForm.occupation}
            onChange={(e) => setEditForm({ ...editForm, occupation: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
        <FormField label="Marital Status">
          <Select
            value={editForm.marital_status || undefined}
            onValueChange={(v) => setEditForm({ ...editForm, marital_status: v })}
          >
            <SelectTrigger className="rounded-sm h-9"><SelectValue placeholder="Select status" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="Single">Single</SelectItem>
              <SelectItem value="Married">Married</SelectItem>
              <SelectItem value="Divorced">Divorced</SelectItem>
              <SelectItem value="Widowed">Widowed</SelectItem>
            </SelectContent>
          </Select>
        </FormField>
        <FormField label="Gov ID Type">
          <Select
            value={editForm.gov_id_type || undefined}
            onValueChange={(v) => setEditForm({ ...editForm, gov_id_type: v })}
          >
            <SelectTrigger className="rounded-sm h-9"><SelectValue placeholder="Select ID type" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="Aadhaar">Aadhaar</SelectItem>
              <SelectItem value="PAN">PAN</SelectItem>
              <SelectItem value="Passport">Passport</SelectItem>
              <SelectItem value="DrivingLicense">Driving License</SelectItem>
              <SelectItem value="Other">Other</SelectItem>
            </SelectContent>
          </Select>
        </FormField>
        <FormField label="Gov ID Number">
          <Input
            value={editForm.gov_id_number}
            onChange={(e) => setEditForm({ ...editForm, gov_id_number: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
      </TabsContent>

      <TabsContent value="address" className="space-y-3" role="group" aria-label="Address">
        <FormField label="Address Line 1">
          <Input
            value={editForm.address_line1}
            onChange={(e) => setEditForm({ ...editForm, address_line1: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
        <FormField label="Address Line 2">
          <Input
            value={editForm.address_line2}
            onChange={(e) => setEditForm({ ...editForm, address_line2: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
        <FormField label="City">
          <Input
            value={editForm.city}
            onChange={(e) => setEditForm({ ...editForm, city: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
        <FormField label="State">
          <Input
            value={editForm.state}
            onChange={(e) => setEditForm({ ...editForm, state: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
        <FormField label="Pincode">
          <Input
            value={editForm.pincode}
            onChange={(e) => setEditForm({ ...editForm, pincode: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
      </TabsContent>

      <TabsContent value="emergency" className="space-y-3" role="group" aria-label="Emergency contact">
        <FormField label="Emergency Contact Name">
          <Input
            value={editForm.emergency_contact_name}
            onChange={(e) => setEditForm({ ...editForm, emergency_contact_name: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
        <FormField label="Emergency Contact Phone">
          <Input
            value={editForm.emergency_contact_phone}
            onChange={(e) => setEditForm({ ...editForm, emergency_contact_phone: e.target.value })}
            className="rounded-sm"
          />
        </FormField>
        <FormField label="Relationship">
          <Select
            value={editForm.emergency_contact_relation || undefined}
            onValueChange={(v) => setEditForm({ ...editForm, emergency_contact_relation: v })}
          >
            <SelectTrigger className="rounded-sm h-9"><SelectValue placeholder="Select relationship" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="Father">Father</SelectItem>
              <SelectItem value="Mother">Mother</SelectItem>
              <SelectItem value="Spouse">Spouse</SelectItem>
              <SelectItem value="Child">Child</SelectItem>
              <SelectItem value="Sibling">Sibling</SelectItem>
              <SelectItem value="Friend">Friend</SelectItem>
              <SelectItem value="Other">Other</SelectItem>
            </SelectContent>
          </Select>
        </FormField>
      </TabsContent>
    </Tabs>

    <Button
      data-testid="pd-save-btn"
      onClick={onSave}
      className="w-full rounded-xl bg-[#064E3B] hover:bg-[#022C22] h-10 min-h-[44px] mt-2"
    >
      Save changes
    </Button>
  </div>
  );
};

const SectionHeading = ({ children }) => (
  <div className="text-[10px] uppercase tracking-[0.1em] text-text-muted font-semibold mb-2">
    {children}
  </div>
);

const VISIT_STATUS_LABELS = {
  scheduled: "Scheduled",
  in_progress: "In Progress",
  completed: "Completed",
  cancelled: "Cancelled",
  no_show: "No Show",
};

const TIMELINE_ICONS = {
  visit: Stethoscope,
  vitals: Heartbeat,
  clinical_note: Note,
  prescription: Pill,
  injection: Syringe,
  appointment: CalendarBlank,
  lab_report: Flask,
};

const MedicalTimeline = ({ patientId, data, enabled = true }) => {
  const [visits, setVisits] = useState([]);
  const [timeline, setTimeline] = useState([]);
  const [visitDetail, setVisitDetail] = useState(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  useEffect(() => {
    if (!enabled) return;
    const controller = new AbortController();
    const { signal } = controller;
    api.get(`/patients/${patientId}/visits`, { signal })
      .then((r) => setVisits(r.data || []))
      .catch(() => { if (!signal.aborted) setVisits([]); });
    api.get(`/patients/${patientId}/timeline`, { signal })
      .then((r) => setTimeline(r.data || []))
      .catch(() => { if (!signal.aborted) setTimeline([]); });
    return () => controller.abort();
  }, [patientId, enabled]);

  const openVisit = async (visitId) => {
    try {
      const r = await api.get(`/visits/${visitId}`);
      setVisitDetail(r.data);
      setDrawerOpen(true);
    } catch (e) {
      toast.error(normalizeApiError(e, "Could not load visit"));
    }
  };

  const printRx = async (rxId) => {
    try {
      await openAuthorizedHtml(`/prescriptions/${rxId}/print`);
    } catch (e) {
      toast.error(normalizeApiError(e, "Could not open prescription"));
    }
  };

  const downloadLabReport = async (reportId, testName) => {
    try {
      const safeName = (testName || "lab-report").replace(/[^\w\s-]/g, "").trim() || "lab-report";
      await downloadAuthorizedFile(`/lab-reports/${reportId}/download`, `${safeName}.pdf`);
    } catch (e) {
      toast.error(normalizeApiError(e, "Could not download lab report"));
    }
  };

  return (
    <div className="space-y-4">
      {(data.appointments || []).length > 0 && (
        <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl">
          <div className="px-5 py-3 border-b border-white/60 flex items-center gap-2">
            <CalendarBlank weight="regular" className="w-4 h-4 text-[#064E3B]" />
            <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">All Appointments</h3>
          </div>
          <div className="p-5 space-y-2 max-h-[320px] overflow-y-auto scrollbar-thin">
            {(data.appointments || []).map((a) => (
              <div key={a.id} className="border border-subtle rounded-sm p-3 text-[12px]">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="font-medium text-[#022C22]">
                    {new Date(a.scheduled_at).toLocaleString()}
                    {" · "}{a.doctor_name || "—"}
                  </div>
                  <span className="text-[10px] uppercase tracking-wider font-semibold px-2 py-0.5 rounded-sm border border-subtle bg-secondary/30 capitalize">
                    {(a.status || "scheduled").replace(/_/g, " ")}
                  </span>
                </div>
                <div className="text-text-secondary mt-0.5">{a.department || "—"}</div>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl">
        <div className="px-5 py-3 border-b border-white/60 flex items-center gap-2">
          <Stethoscope weight="regular" className="w-4 h-4 text-[#064E3B]" />
          <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">Visit History</h3>
        </div>
        <div className="p-5 space-y-2 max-h-[320px] overflow-y-auto scrollbar-thin">
          {visits.length === 0 && (
            <div className="text-center py-6 text-text-muted text-[13px]">No visits recorded yet</div>
          )}
          {visits.map((v) => (
            <button
              key={v.id}
              type="button"
              onClick={() => openVisit(v.id)}
              className="w-full text-left border border-subtle rounded-sm p-3 hover:bg-secondary/30 transition-colors"
            >
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <div className="text-[13px] font-semibold text-[#022C22]">
                    {new Date(v.visit_date).toLocaleDateString(undefined, { weekday: "short", year: "numeric", month: "short", day: "numeric" })}
                    {" · "}{v.doctor_name}
                  </div>
                  <div className="text-[12px] text-text-secondary mt-0.5">
                    {v.department || "—"} · {v.diagnosis || v.symptoms || "No diagnosis recorded"}
                  </div>
                </div>
                <span className="text-[10px] uppercase tracking-wider font-semibold px-2 py-0.5 rounded-sm border border-subtle bg-secondary/30">
                  {VISIT_STATUS_LABELS[v.status] || v.status}
                </span>
              </div>
            </button>
          ))}
        </div>
      </div>

      <div className="border border-white/60 bg-white/70 backdrop-blur-sm rounded-xl">
        <div className="px-5 py-3 border-b border-white/60 flex items-center gap-2">
          <ClockCounterClockwise weight="regular" className="w-4 h-4 text-text-muted" />
          <h3 className="font-heading text-[14px] font-semibold text-[#022C22]">Unified Medical Timeline</h3>
        </div>
        <div className="p-5 space-y-3 max-h-[480px] overflow-y-auto scrollbar-thin">
          {timeline.length === 0 && (
            <div className="text-center py-8 text-text-muted text-[13px]">No medical activity yet</div>
          )}
          {timeline.map((entry, idx) => {
            const Icon = TIMELINE_ICONS[entry.type] || Note;
            return (
              <div key={`${entry.type}-${entry.entity_id}-${idx}`} className="flex gap-3 pb-3 border-b border-subtle last:border-0">
                <div className="w-7 h-7 shrink-0 rounded-full bg-[#064E3B]/10 text-[#064E3B] flex items-center justify-center">
                  <Icon weight="regular" className="w-3.5 h-3.5" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-[12px] text-text-secondary">
                    <span className="font-medium text-[#022C22]">{entry.title}</span>
                    <span className="text-text-muted"> · {new Date(entry.occurred_at).toLocaleString()}</span>
                  </div>
                  {entry.summary && (
                    <div className="text-[13px] text-[#022C22] mt-1 line-clamp-2">{entry.summary}</div>
                  )}
                  {entry.type === "visit" && entry.entity_id && (
                    <button type="button" onClick={() => openVisit(entry.entity_id)} className="text-[11px] text-[#064E3B] mt-1 hover:underline">
                      View visit details
                    </button>
                  )}
                  {entry.type === "prescription" && entry.entity_id && (
                    <button type="button" onClick={() => printRx(entry.entity_id)} className="text-[11px] text-[#064E3B] mt-1 hover:underline">
                      Print prescription
                    </button>
                  )}
                  {entry.type === "lab_report" && entry.entity_id && (
                    <button
                      type="button"
                      onClick={() => downloadLabReport(entry.entity_id, entry.title)}
                      className="text-[11px] text-[#064E3B] mt-1 hover:underline"
                    >
                      Download lab report
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      <Sheet open={drawerOpen} onOpenChange={setDrawerOpen}>
        <SheetContent className="w-full sm:max-w-lg overflow-y-auto">
          {visitDetail && (
            <>
              <SheetHeader>
                <SheetTitle className="font-heading">Visit Details</SheetTitle>
                <SheetDescription>
                  {new Date(visitDetail.visit_date).toLocaleString()} · {visitDetail.doctor_name}
                </SheetDescription>
              </SheetHeader>
              <div className="mt-6 space-y-4 text-[13px]">
                <DetailBlock label="Status" value={VISIT_STATUS_LABELS[visitDetail.status] || visitDetail.status} />
                <DetailBlock label="Department" value={visitDetail.department} />
                <DetailBlock label="Symptoms" value={visitDetail.symptoms || visitDetail.chief_complaint} />
                <DetailBlock label="Diagnosis" value={visitDetail.diagnosis} />
                <DetailBlock label="Doctor notes" value={visitDetail.doctor_notes} />
                <DetailBlock label="Follow-up advice" value={visitDetail.follow_up_advice} />
                {visitDetail.follow_up_date && (
                  <DetailBlock label="Follow-up date" value={new Date(visitDetail.follow_up_date).toLocaleDateString()} />
                )}
                {(visitDetail.vitals || []).length > 0 && (
                  <div>
                    <SectionHeading>Vitals</SectionHeading>
                    <div className="space-y-2">
                      {visitDetail.vitals.map((v) => (
                        <div key={v.id} className="border border-subtle rounded-sm p-2 text-[12px]">
                          BP {v.systolic_bp || "—"}/{v.diastolic_bp || "—"}
                          {v.heart_rate ? ` · HR ${v.heart_rate}` : ""}
                        </div>
                      ))}
                    </div>
                  </div>
                )}
                {(visitDetail.prescriptions || []).length > 0 && (
                  <div>
                    <SectionHeading>Prescriptions</SectionHeading>
                    {visitDetail.prescriptions.map((rx) => (
                      <div key={rx.id} className="flex items-center justify-between border border-subtle rounded-sm p-2 mb-2">
                        <span className="text-[12px]">{rx.diagnosis || `${rx.items?.length || 0} items`}</span>
                        <button type="button" onClick={() => printRx(rx.id)} className="text-[11px] text-[#064E3B] hover:underline">
                          Print
                        </button>
                      </div>
                    ))}
                  </div>
                )}
                {(visitDetail.lab_reports || []).length > 0 && (
                  <div>
                    <SectionHeading>Lab Reports</SectionHeading>
                    <div className="space-y-2">
                      {visitDetail.lab_reports.map((lab) => (
                        <div key={lab.id} className="flex items-center justify-between border border-subtle rounded-sm p-2">
                          <span className="text-[12px]">{lab.test_name || lab.testName || "Lab report"}</span>
                          <button
                            type="button"
                            onClick={() => downloadLabReport(lab.id, lab.test_name || lab.testName)}
                            className="text-[11px] text-[#064E3B] hover:underline"
                          >
                            Download
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>
    </div>
  );
};

const DetailBlock = ({ label, value, muted }) => (
  value ? (
    <div>
      <SectionHeading>{label}</SectionHeading>
      <div className={`text-[#022C22] whitespace-pre-wrap ${muted ? "text-text-muted italic" : ""}`}>{value}</div>
    </div>
  ) : null
);

const FormField = ({ label, children }) => (
  <div>
    <label className="text-[10px] uppercase tracking-[0.1em] text-text-secondary font-semibold block mb-1">
      {label}
    </label>
    {children}
  </div>
);

export default PatientDetail;

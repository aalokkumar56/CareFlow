import { useCallback, useEffect, useMemo, useState } from "react";
import { api, normalizeApiError } from "@/lib/api";
import { toast } from "sonner";

export function useConsultationSession({
  patientId,
  appointmentId,
  appointments,
  canEditAppointment,
  onRefresh,
}) {
  const [visitId, setVisitId] = useState(null);
  const [starting, setStarting] = useState(false);
  const [ready, setReady] = useState(false);

  const appointment = useMemo(
    () => (appointments || []).find((a) => String(a.id) === String(appointmentId)),
    [appointments, appointmentId],
  );

  useEffect(() => {
    if (!patientId || !appointmentId || !appointment) return undefined;

    let cancelled = false;

    const start = async () => {
      setStarting(true);
      try {
        if (appointment.status === "scheduled" && canEditAppointment) {
          await api.patch(`/appointments/${appointmentId}/status`, { status: "confirmed" });
          if (!cancelled) onRefresh?.();
        }

        const res = await api.post(`/patients/${patientId}/visits`, {
          patient_id: patientId,
          appointment_id: appointmentId,
          doctor_user_id: appointment.doctor_user_id || undefined,
          department: appointment.department || undefined,
          symptoms: appointment.chief_complaint || appointment.notes || undefined,
          visit_type: "outpatient",
        });

        if (cancelled) return;
        setVisitId(res.data?.id ?? null);
        setReady(true);
      } catch (error) {
        if (cancelled) return;
        toast.error(normalizeApiError(error, "Could not start consultation"));
      } finally {
        if (!cancelled) setStarting(false);
      }
    };

    start();
    return () => {
      cancelled = true;
    };
  }, [patientId, appointmentId, appointment, canEditAppointment, onRefresh]);

  const completeConsultation = useCallback(async () => {
    try {
      if (visitId) {
        await api.post(`/visits/${visitId}/complete`);
      }
      if (appointmentId && canEditAppointment) {
        await api.patch(`/appointments/${appointmentId}/status`, { status: "completed" });
      }
      toast.success("Consultation completed");
      onRefresh?.();
      return true;
    } catch (error) {
      toast.error(normalizeApiError(error, "Could not complete consultation"));
      return false;
    }
  }, [visitId, appointmentId, canEditAppointment, onRefresh]);

  return {
    visitId,
    appointment,
    starting,
    ready,
    completeConsultation,
  };
}

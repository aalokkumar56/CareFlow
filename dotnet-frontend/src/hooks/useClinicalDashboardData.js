import { useEffect, useState } from "react";
import { apiGet, toastApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";

export function useClinicalDashboardData({ date, scope, doctorUserId } = {}) {
  const { user } = useAuth();
  const { canFetch } = usePermissions();
  const canView = canFetch(PERMISSIONS.DashboardView)
    && canFetch(PERMISSIONS.AppointmentView)
    && canFetch(PERMISSIONS.ClinicalView);
  const [overview, setOverview] = useState(null);
  const [loading, setLoading] = useState(true);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    if (!user || !canView) {
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    let cancelled = false;

    const params = new URLSearchParams();
    if (date) params.set("date", date);
    if (scope) params.set("scope", scope);
    if (doctorUserId) params.set("doctor_user_id", doctorUserId);

    const load = async () => {
      setLoading(true);
      try {
        const qs = params.toString();
        const data = await apiGet(`/dashboard/clinical-overview${qs ? `?${qs}` : ""}`, {
          signal: controller.signal,
        });
        if (!cancelled) setOverview(data);
      } catch (error) {
        if (cancelled || controller.signal.aborted) return;
        toastApiError(error, "Failed to load clinical dashboard");
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    load();
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [user, canView, date, scope, doctorUserId, reloadToken]);

  return { overview, loading, canView, reload: () => setReloadToken((n) => n + 1) };
}

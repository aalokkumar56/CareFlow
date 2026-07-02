import { useEffect, useState } from "react";
import { apiGet, toastApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";

export const useDashboardData = () => {
  const { user, ready } = useAuth();
  const { canFetch } = usePermissions();
  const canViewDashboard = canFetch(PERMISSIONS.DashboardView);
  const [overview, setOverview] = useState(null);
  const [missedRevenue, setMissedRevenue] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!ready || !user) {
      if (ready && !user) setLoading(false);
      return;
    }
    if (!canViewDashboard) {
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    let cancelled = false;

    const load = async () => {
      setLoading(true);
      try {
        const [overviewRes, missedRes] = await Promise.all([
          apiGet("/dashboard/overview", { signal: controller.signal }),
          apiGet("/dashboard/missed-revenue", { signal: controller.signal }),
        ]);
        if (!cancelled) {
          setOverview(overviewRes);
          setMissedRevenue(missedRes);
        }
      } catch (error) {
        if (cancelled || controller.signal.aborted) return;
        toastApiError(error, "Failed to load dashboard");
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    load();
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [ready, user, canViewDashboard]);

  return { overview, missedRevenue, loading };
};

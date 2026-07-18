import React, { useEffect, useMemo, useState } from "react";
import GlassCard from "@/components/glass/GlassCard";
import FormField from "@/components/forms/FormField";
import { api, normalizeApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";
import { useAuth } from "@/lib/auth";
import {
  DEFAULT_HOSPITAL_TIMEZONE,
  resolveHospitalTimezone,
} from "@/lib/tenantTime";
import {
  HOSPITAL_TIMEZONES,
  detectBrowserTimezone,
  labelForTimezone,
} from "@/lib/hospitalTimezones";

const DISMISS_KEY = "cureflow_tz_suggest_dismissed";

const HospitalTimezonePanel = () => {
  const { tenant, refreshSession } = useAuth();
  const { can } = usePermissions();
  const canEdit = can(PERMISSIONS.SettingsEdit);

  const [timezone, setTimezone] = useState(DEFAULT_HOSPITAL_TIMEZONE);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [suggestDismissed, setSuggestDismissed] = useState(() => {
    if (typeof window === "undefined") return true;
    try {
      return sessionStorage.getItem(DISMISS_KEY) === "1";
    } catch {
      return false;
    }
  });

  const browserTz = useMemo(() => detectBrowserTimezone(), []);

  const load = () => {
    setLoading(true);
    api.get("/hospital-profile")
      .then((r) => {
        const tz = r.data?.timezone || resolveHospitalTimezone(tenant) || DEFAULT_HOSPITAL_TIMEZONE;
        setTimezone(tz);
      })
      .catch(() => {
        setTimezone(resolveHospitalTimezone(tenant));
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, []);

  const options = useMemo(() => {
    const list = [...HOSPITAL_TIMEZONES];
    if (timezone && !list.some((z) => z.value === timezone)) {
      list.unshift({ value: timezone, label: timezone });
    }
    if (browserTz && !list.some((z) => z.value === browserTz)) {
      list.push({ value: browserTz, label: `${browserTz} (from browser)` });
    }
    return list;
  }, [timezone, browserTz]);

  const showSuggest =
    canEdit
    && !suggestDismissed
    && browserTz
    && browserTz !== timezone;

  const dismissSuggest = () => {
    try {
      sessionStorage.setItem(DISMISS_KEY, "1");
    } catch { /* ignore */ }
    setSuggestDismissed(true);
  };

  const save = async (nextTz = timezone) => {
    if (!canEdit) return;
    setSaving(true);
    try {
      await api.put("/hospital-profile", { timezone: nextTz });
      setTimezone(nextTz);
      toast.success("Hospital timezone saved");
      await refreshSession();
      load();
    } catch (e) {
      toast.error(normalizeApiError(e, "Failed to save timezone"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <GlassCard padding={false} className="p-4 sm:p-5 mb-4" data-testid="hospital-timezone-panel">
      <div className="mb-4">
        <h2 className="font-heading text-[15px] font-semibold text-[#022C22]" data-testid="hospital-timezone-heading">
          Timezone
        </h2>
        <p className="text-[12px] text-text-secondary mt-0.5">
          Appointment booking and WhatsApp messages use this hospital timezone (shown with a zone label, e.g. 11:00 AM IST).
        </p>
      </div>

      {loading ? (
        <p className="text-[13px] text-text-muted">Loading…</p>
      ) : (
        <>
          {showSuggest && (
            <div
              className="mb-4 rounded-xl border border-indigo-200 bg-indigo-50/80 px-3 py-2.5 text-[12px] text-[#022C22]"
              data-testid="hospital-timezone-suggest"
            >
              <p>
                Your browser is set to <span className="font-semibold">{labelForTimezone(browserTz)}</span>.
                Use this as the hospital timezone?
              </p>
              <div className="flex flex-wrap gap-2 mt-2">
                <Button
                  type="button"
                  size="sm"
                  className="rounded-xl h-8"
                  disabled={saving}
                  data-testid="hospital-timezone-use-browser"
                  onClick={() => save(browserTz)}
                >
                  Use this
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  className="rounded-xl h-8"
                  data-testid="hospital-timezone-dismiss-suggest"
                  onClick={dismissSuggest}
                >
                  Dismiss
                </Button>
              </div>
            </div>
          )}

          <FormField label="Hospital timezone" className="max-w-lg mb-4">
            <Select
              value={timezone}
              onValueChange={setTimezone}
              disabled={!canEdit || saving}
            >
              <SelectTrigger className="rounded-xl h-9" data-testid="hospital-timezone-select">
                <SelectValue placeholder="Select timezone" />
              </SelectTrigger>
              <SelectContent>
                {options.map((z) => (
                  <SelectItem key={z.value} value={z.value}>
                    {z.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FormField>

          {canEdit && (
            <Button
              type="button"
              onClick={() => save()}
              disabled={saving}
              className="btn-primary rounded-xl h-9"
              data-testid="hospital-timezone-save"
            >
              {saving ? "Saving…" : "Save timezone"}
            </Button>
          )}
        </>
      )}
    </GlassCard>
  );
};

export default HospitalTimezonePanel;

import React, { useEffect, useState } from "react";
import { Link, useNavigate } from "@/lib/navigation";
import { api, normalizeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { Buildings, WhatsappLogo, UsersThree, CheckCircle, ArrowSquareOut } from "@phosphor-icons/react";

const steps = [
  {
    key: "profile",
    label: "Hospital profile",
    description: "Add your hospital name, address, and contact details.",
    icon: Buildings,
    endpoint: "profile",
    href: "/settings/hospital",
    linkLabel: "Open hospital settings",
  },
  {
    key: "whatsapp",
    label: "WhatsApp setup",
    description: "Connect WhatsApp Business so patients can message your hospital.",
    icon: WhatsappLogo,
    endpoint: "whatsapp",
    href: "/settings/integrations",
    linkLabel: "Open integrations",
  },
  {
    key: "team",
    label: "Invite team",
    description: "Add doctors and staff who will use CureFlow day to day.",
    icon: UsersThree,
    endpoint: "team",
    href: "/settings/users",
    linkLabel: "Manage users",
  },
];

const Onboarding = () => {
  const navigate = useNavigate();
  const { refreshSession, tenant } = useAuth();
  const [state, setState] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [busy, setBusy] = useState(null);

  const load = async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const res = await api.get("/onboarding");
      setState(res.data);
    } catch (err) {
      setLoadError(normalizeApiError(err, "Could not load onboarding"));
      toast.error(normalizeApiError(err, "Could not load onboarding"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const completeStep = async (endpoint) => {
    setBusy(endpoint);
    try {
      const res = await api.post(`/onboarding/${endpoint}`);
      setState(res.data);
      toast.success("Step saved");
    } catch (err) {
      toast.error(normalizeApiError(err, "Step failed"));
    } finally {
      setBusy(null);
    }
  };

  const finish = async () => {
    setBusy("complete");
    try {
      await api.post("/onboarding/complete");
      await refreshSession();
      toast.success("Welcome to CureFlow!");
      navigate("/");
    } catch (err) {
      toast.error(normalizeApiError(err, "Could not finish onboarding"));
    } finally {
      setBusy(null);
    }
  };

  const stepDone = {
    profile: state?.profile_complete ?? state?.profileComplete,
    whatsapp: state?.whatsapp_connected ?? state?.whatsAppConnected,
    team: state?.team_invited ?? state?.teamInvited,
  };

  const allStepsDone =
    state?.is_complete ||
    state?.isComplete ||
    (stepDone.profile && stepDone.whatsapp && stepDone.team);

  const hospitalName = tenant?.name || "your hospital";

  return (
    <div className="min-h-screen bg-background p-6 sm:p-10" data-testid="onboarding-page">
      <div className="max-w-2xl mx-auto">
        <h1 className="font-heading text-2xl font-semibold mb-2" data-testid="onboarding-title">
          Welcome — set up {hospitalName}
        </h1>
        <p className="text-sm text-muted-foreground mb-2">
          Complete these steps to unlock your dashboard, inbox, and patient CRM.
        </p>
        <p className="text-xs text-muted-foreground mb-8">
          Configure each item in Settings, then mark it done — or skip setup and finish later.
        </p>

        {loadError && (
          <div className="glass-card p-4 mb-6 text-sm text-red-700 bg-red-50/80 border border-red-200">
            {loadError}
            <Button variant="link" className="px-0 h-auto ml-2" onClick={load}>
              Retry
            </Button>
          </div>
        )}

        <ul className="space-y-4 mb-8">
          {steps.map(({ key, label, description, icon: Icon, endpoint, href, linkLabel }) => (
            <li
              key={key}
              className="glass-card p-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4"
              data-testid={`onboarding-step-${key}`}
            >
              <div className="flex items-start gap-3 min-w-0">
                <Icon className="w-6 h-6 text-primary shrink-0 mt-0.5" weight="duotone" />
                <div className="min-w-0">
                  <p className="font-medium">{label}</p>
                  <p className="text-xs text-muted-foreground mt-1">{description}</p>
                  <Link
                    href={href}
                    className="inline-flex items-center gap-1 text-xs text-primary hover:underline mt-2"
                    data-testid={`onboarding-link-${key}`}
                  >
                    {linkLabel}
                    <ArrowSquareOut className="w-3 h-3" />
                  </Link>
                  {stepDone[key] && (
                    <p className="text-xs text-emerald-600 flex items-center gap-1 mt-2">
                      <CheckCircle className="w-3 h-3" /> Done
                    </p>
                  )}
                </div>
              </div>
              <Button
                size="sm"
                variant={stepDone[key] ? "outline" : "default"}
                disabled={loading || busy === endpoint || stepDone[key]}
                onClick={() => completeStep(endpoint)}
                data-testid={`onboarding-complete-${key}`}
                className="shrink-0"
              >
                {stepDone[key] ? "Completed" : busy === endpoint ? "Saving..." : "Mark done"}
              </Button>
            </li>
          ))}
        </ul>

        <div className="flex flex-col sm:flex-row gap-3">
          <Button
            className="w-full sm:w-auto"
            disabled={loading || busy === "complete"}
            onClick={finish}
            data-testid="onboarding-finish"
          >
            {busy === "complete" ? "Finishing..." : allStepsDone ? "Go to dashboard" : "Continue to dashboard"}
          </Button>
          {!allStepsDone && (
            <p className="text-xs text-muted-foreground sm:self-center">
              You can finish WhatsApp and team setup later in Settings.
            </p>
          )}
        </div>
      </div>
    </div>
  );
};

export default Onboarding;

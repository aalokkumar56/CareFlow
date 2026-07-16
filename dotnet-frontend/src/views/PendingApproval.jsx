import React, { useCallback, useState } from "react";
import { useAuth } from "@/lib/auth";
import { lifecycleRouteForTenant } from "@/lib/tenantBranding";
import { HourglassMedium } from "@phosphor-icons/react";
import { Button } from "@/components/ui/button";
import CureFlowMark from "@/components/brand/CureFlowMark";

const PendingApproval = () => {
  const { tenant, logout, refreshSession } = useAuth();
  const [checking, setChecking] = useState(false);
  const hospitalName = tenant?.name || "Your hospital";

  const checkApproval = useCallback(async () => {
    setChecking(true);
    try {
      const session = await refreshSession();
      const route = lifecycleRouteForTenant(session.tenant);
      if (route !== "/pending-approval") {
        window.location.href = route;
      }
    } catch {
      /* session refresh failed — stay on waiting room */
    } finally {
      setChecking(false);
    }
  }, [refreshSession]);

  return (
    <div className="min-h-screen bg-background flex items-center justify-center p-6" data-testid="pending-approval-page">
      <div className="max-w-md w-full glass-card p-8 text-center">
        <div className="flex justify-center mb-4">
          <HourglassMedium className="w-12 h-12 text-amber-500" weight="duotone" />
        </div>
        <CureFlowMark className="mb-4" />
        <h1 className="font-heading text-xl font-semibold mb-2" data-testid="pending-approval-title">
          Awaiting CureFlow approval
        </h1>
        <p className="text-sm text-muted-foreground mb-4">
          <strong>{hospitalName}</strong> is registered and pending review by the CureFlow team.
          You are signed in, but CRM access opens after approval.
        </p>
        <p className="text-xs text-muted-foreground mb-6">
          Typical review time: 1–2 business days. We will email you at{" "}
          {tenant?.contact_email || tenant?.contactEmail || "your admin email"}.
        </p>
        <div className="flex flex-col gap-2">
          <Button
            onClick={checkApproval}
            disabled={checking}
            data-testid="pending-approval-check"
          >
            {checking ? "Checking..." : "Check approval status"}
          </Button>
          <Button variant="outline" onClick={logout} data-testid="pending-approval-logout">
            Sign out
          </Button>
        </div>
        <p className="text-xs text-muted-foreground mt-4">
          Already approved? Click &quot;Check approval status&quot; above, or sign out and sign in again.
        </p>
      </div>
    </div>
  );
};

export default PendingApproval;

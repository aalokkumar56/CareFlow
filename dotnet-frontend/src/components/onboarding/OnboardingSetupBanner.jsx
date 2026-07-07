"use client";

import { Link } from "@/lib/navigation";
import { useAuth } from "@/lib/auth";
import { lifecycleRouteForTenant } from "@/lib/tenantBranding";
import { ArrowLeft } from "@phosphor-icons/react";

const OnboardingSetupBanner = () => {
  const { tenant } = useAuth();
  if (lifecycleRouteForTenant(tenant) !== "/onboarding") return null;

  return (
    <div
      className="mb-4 glass-card px-4 py-3 flex items-center justify-between gap-3"
      data-testid="onboarding-setup-banner"
    >
      <p className="text-sm text-muted-foreground">
        Finish hospital setup — configure this section, then return to onboarding.
      </p>
      <Link
        href="/onboarding"
        className="inline-flex items-center gap-1 text-sm text-primary font-medium shrink-0 hover:underline"
        data-testid="onboarding-back-link"
      >
        <ArrowLeft className="w-4 h-4" />
        Back to setup
      </Link>
    </div>
  );
};

export default OnboardingSetupBanner;

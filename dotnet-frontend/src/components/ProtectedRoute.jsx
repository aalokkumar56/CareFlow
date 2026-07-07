"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/lib/auth";
import { isOnboardingSetupPath, lifecycleRouteForTenant } from "@/lib/tenantBranding";
import { Navigate, usePathname } from "@/lib/navigation";
import RouteLoadingSkeleton from "@/components/RouteLoadingSkeleton";

const LIFECYCLE_PATHS = ["/pending-approval", "/onboarding", "/registration-received"];

const ProtectedRoute = ({ children }) => {
  const { user, tenant, loading } = useAuth();
  const pathname = usePathname();
  const [clientReady, setClientReady] = useState(false);

  useEffect(() => {
    setClientReady(true);
  }, []);

  if (!clientReady) {
    return <RouteLoadingSkeleton />;
  }

  if (loading && !user) {
    return (
      <div className="h-screen w-screen flex items-center justify-center bg-background">
        <div className="text-text-muted text-sm">Loading...</div>
      </div>
    );
  }

  if (!user) return <Navigate to="/login" replace />;

  const lifecycleRoute = lifecycleRouteForTenant(tenant);
  const onLifecyclePage = LIFECYCLE_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`));
  const onOnboardingSetupPage = isOnboardingSetupPath(pathname);
  const allowedDuringLifecycle =
    onLifecyclePage || (lifecycleRoute === "/onboarding" && onOnboardingSetupPage);

  if (lifecycleRoute !== "/" && !allowedDuringLifecycle && pathname !== lifecycleRoute) {
    return <Navigate to={lifecycleRoute} replace />;
  }

  if (lifecycleRoute === "/" && onLifecyclePage) {
    return <Navigate to="/" replace />;
  }

  return children;
};

export default ProtectedRoute;

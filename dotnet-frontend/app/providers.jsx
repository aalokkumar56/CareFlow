"use client";

import { useEffect } from "react";
import { AuthProvider } from "@/lib/auth";
import ErrorBoundary from "@/components/ErrorBoundary";
import GlobalLoader from "@/components/GlobalLoader";
import RoutePrefetcher from "@/components/RoutePrefetcher";
import { Toaster } from "@/components/ui/sonner";
import { initClientLogger } from "@/lib/logger";

export default function Providers({ children }) {
  useEffect(() => {
    initClientLogger();
  }, []);

  return (
    <ErrorBoundary>
      <AuthProvider>
        <RoutePrefetcher />
        <GlobalLoader />
        <Toaster position="top-right" />
        {children}
      </AuthProvider>
    </ErrorBoundary>
  );
}

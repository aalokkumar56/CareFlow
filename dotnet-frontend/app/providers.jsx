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
    // #region agent log
    const navStart = performance.now();
    fetch('http://127.0.0.1:7396/ingest/71a493aa-be86-4272-b3f4-088f0dfe3f3f',{method:'POST',headers:{'Content-Type':'application/json','X-Debug-Session-Id':'a6e1ca'},body:JSON.stringify({sessionId:'a6e1ca',location:'providers.jsx:mount',message:'app_mount',data:{path:window.location.pathname,dev:process.env.NODE_ENV==='development'},timestamp:Date.now(),hypothesisId:'H4'})}).catch(()=>{});
    const onLoad = () => {
      fetch('http://127.0.0.1:7396/ingest/71a493aa-be86-4272-b3f4-088f0dfe3f3f',{method:'POST',headers:{'Content-Type':'application/json','X-Debug-Session-Id':'a6e1ca'},body:JSON.stringify({sessionId:'a6e1ca',location:'providers.jsx:window.load',message:'window_load',data:{elapsedMs:Math.round(performance.now()-navStart),path:window.location.pathname},timestamp:Date.now(),hypothesisId:'H4'})}).catch(()=>{});
    };
    if (document.readyState === 'complete') onLoad();
    else window.addEventListener('load', onLoad, { once: true });
    return () => window.removeEventListener('load', onLoad);
    // #endregion
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

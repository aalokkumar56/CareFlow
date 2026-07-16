"use client";

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { getPrefetchPathsForUser } from "@/lib/prefetchRoutes";

/**
 * After login (or session restore), prefetch permitted routes in the background
 * so the first sidebar click feels instant.
 */
const RoutePrefetcher = () => {
  const { user, ready } = useAuth();
  const router = useRouter();
  const prefetchedKey = useRef(null);

  useEffect(() => {
    if (!ready || !user) return;

    const key = user.id || user.email || user.sub;
    if (!key || prefetchedKey.current === key) return;
    prefetchedKey.current = key;

    // Next.js dev compiles every prefetched route — 19 routes caused major slowdown.
    if (process.env.NODE_ENV === "development") {
      // #region agent log
      fetch('http://127.0.0.1:7396/ingest/71a493aa-be86-4272-b3f4-088f0dfe3f3f',{method:'POST',headers:{'Content-Type':'application/json','X-Debug-Session-Id':'a6e1ca'},body:JSON.stringify({sessionId:'a6e1ca',runId:'post-fix',location:'RoutePrefetcher.jsx:effect',message:'prefetch_skipped_dev',data:{reason:'dev_mode'},timestamp:Date.now(),hypothesisId:'H1'})}).catch(()=>{});
      // #endregion
      return;
    }

    const paths = getPrefetchPathsForUser(user).slice(0, 5);
    if (!paths.length) return;

    const prefetchAll = () => {
      const t0 = performance.now();
      // #region agent log
      fetch('http://127.0.0.1:7396/ingest/71a493aa-be86-4272-b3f4-088f0dfe3f3f',{method:'POST',headers:{'Content-Type':'application/json','X-Debug-Session-Id':'a6e1ca'},body:JSON.stringify({sessionId:'a6e1ca',location:'RoutePrefetcher.jsx:prefetchAll',message:'prefetch_start',data:{pathCount:paths.length,paths},timestamp:Date.now(),hypothesisId:'H1'})}).catch(()=>{});
      // #endregion
      paths.forEach((path) => {
        try {
          router.prefetch(path);
        } catch {
          /* prefetch is best-effort */
        }
      });
      // #region agent log
      fetch('http://127.0.0.1:7396/ingest/71a493aa-be86-4272-b3f4-088f0dfe3f3f',{method:'POST',headers:{'Content-Type':'application/json','X-Debug-Session-Id':'a6e1ca'},body:JSON.stringify({sessionId:'a6e1ca',location:'RoutePrefetcher.jsx:prefetchAll',message:'prefetch_done',data:{pathCount:paths.length,elapsedMs:Math.round(performance.now()-t0)},timestamp:Date.now(),hypothesisId:'H1'})}).catch(()=>{});
      // #endregion
    };

    // Defer until after paint / dashboard mount so login feels snappy first.
    if (typeof window.requestIdleCallback === "function") {
      window.requestIdleCallback(prefetchAll, { timeout: 2500 });
    } else {
      window.setTimeout(prefetchAll, 150);
    }
  }, [user, ready, router]);

  return null;
};

export default RoutePrefetcher;

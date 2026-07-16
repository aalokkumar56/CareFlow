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

    const paths = getPrefetchPathsForUser(user);
    if (!paths.length) return;

    const prefetchAll = () => {
      paths.forEach((path) => {
        try {
          router.prefetch(path);
        } catch {
          /* prefetch is best-effort */
        }
      });
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

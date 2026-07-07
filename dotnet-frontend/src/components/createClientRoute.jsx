"use client";

import dynamic from "next/dynamic";
import RouteLoadingSkeleton from "@/components/RouteLoadingSkeleton";

/** Client-only route loader — use from `"use client"` page.jsx files only. */
export function createClientRoute(loader) {
  return dynamic(
    () =>
      loader().then((mod) => {
        // #region agent log
        if (typeof window !== "undefined") {
          fetch("http://127.0.0.1:7396/ingest/71a493aa-be86-4272-b3f4-088f0dfe3f3f", {
            method: "POST",
            headers: { "Content-Type": "application/json", "X-Debug-Session-Id": "a6e1ca" },
            body: JSON.stringify({
              sessionId: "a6e1ca",
              runId: "post-fix",
              location: "createClientRoute.jsx:loader",
              message: "client_page_chunk_loaded",
              data: { path: window.location.pathname },
              timestamp: Date.now(),
              hypothesisId: "H6",
            }),
          }).catch(() => {});
        }
        // #endregion
        return mod;
      }),
    {
      ssr: false,
      loading: () => <RouteLoadingSkeleton />,
    },
  );
}

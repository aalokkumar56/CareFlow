"use client";

import dynamic from "next/dynamic";
import RouteLoadingSkeleton from "@/components/RouteLoadingSkeleton";

/** Client-only route loader — use from `"use client"` page.jsx files only. */
export function createClientRoute(loader) {
  return dynamic(loader, {
    ssr: false,
    loading: () => <RouteLoadingSkeleton />,
  });
}

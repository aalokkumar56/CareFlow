import dynamic from "next/dynamic";
import RouteLoadingSkeleton from "@/components/RouteLoadingSkeleton";

/** Code-split heavy view components so route clicks load smaller chunks first. */
export function loadView(importFn) {
  return dynamic(importFn, {
    loading: () => <RouteLoadingSkeleton />,
    ssr: false,
  });
}

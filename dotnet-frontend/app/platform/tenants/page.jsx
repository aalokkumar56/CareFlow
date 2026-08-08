"use client";

import dynamic from "next/dynamic";
import RouteLoadingSkeleton from "@/components/RouteLoadingSkeleton";

const PlatformTenantsPage = dynamic(() => import("./page.client"), {
  ssr: false,
  loading: () => <RouteLoadingSkeleton />,
});

export default PlatformTenantsPage;

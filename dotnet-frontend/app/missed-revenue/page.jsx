"use client";

import dynamic from "next/dynamic";
import RouteLoadingSkeleton from "@/components/RouteLoadingSkeleton";

const MissedRevenuePage = dynamic(() => import("./page.client"), {
  ssr: false,
  loading: () => <RouteLoadingSkeleton />,
});

export default MissedRevenuePage;

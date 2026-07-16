"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const MissedRevenue = loadView(() => import("@/views/MissedRevenue"));

export default function MissedRevenuePage() {
  return (
    <ClientPage permission={PERMISSIONS.DashboardView}>
      <MissedRevenue />
    </ClientPage>
  );
}

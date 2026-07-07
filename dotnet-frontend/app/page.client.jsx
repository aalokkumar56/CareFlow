"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const Dashboard = loadView(() => import("@/views/Dashboard"));

export default function DashboardPage() {
  return (
    <ClientPage permission={PERMISSIONS.DashboardView}>
      <Dashboard />
    </ClientPage>
  );
}

"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const Tasks = loadView(() => import("@/views/Tasks"));

export default function TasksPage() {
  return (
    <ClientPage permission={PERMISSIONS.DashboardView}>
      <Tasks />
    </ClientPage>
  );
}

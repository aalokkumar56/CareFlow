"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const Appointments = loadView(() => import("@/views/Appointments"));

export default function AppointmentsPage() {
  return (
    <ClientPage permission={PERMISSIONS.AppointmentView}>
      <Appointments />
    </ClientPage>
  );
}

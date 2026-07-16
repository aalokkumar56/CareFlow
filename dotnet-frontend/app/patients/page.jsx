"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const Patients = loadView(() => import("@/views/Patients"));

export default function PatientsPage() {
  return (
    <ClientPage permission={PERMISSIONS.PatientView}>
      <Patients />
    </ClientPage>
  );
}

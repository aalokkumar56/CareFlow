"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const PatientDetail = loadView(() => import("@/views/PatientDetail"));

export default function PatientDetailPage() {
  return (
    <ClientPage permission={PERMISSIONS.PatientView}>
      <PatientDetail />
    </ClientPage>
  );
}

"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const HospitalPage = loadView(() => import("@/views/settings/HospitalPage"));

export default function SettingsHospitalPage() {
  return (
    <ClientPage permission={PERMISSIONS.SettingsView}>
      <HospitalPage />
    </ClientPage>
  );
}

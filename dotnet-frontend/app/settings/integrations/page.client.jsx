"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const IntegrationsPage = loadView(() => import("@/views/settings/IntegrationsPage"));

export default function SettingsIntegrationsPage() {
  return (
    <ClientPage permission={PERMISSIONS.SettingsView}>
      <IntegrationsPage />
    </ClientPage>
  );
}

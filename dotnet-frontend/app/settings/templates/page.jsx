"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const TemplatesPage = loadView(() => import("@/views/settings/TemplatesPage"));

export default function SettingsTemplatesPage() {
  return (
    <ClientPage permission={PERMISSIONS.SettingsView}>
      <TemplatesPage />
    </ClientPage>
  );
}

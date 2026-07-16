"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const Settings = loadView(() => import("@/views/Settings"));

export default function SettingsPage() {
  return (
    <ClientPage permission={PERMISSIONS.SettingsView}>
      <Settings />
    </ClientPage>
  );
}

"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const NotificationsPage = loadView(() => import("@/views/settings/NotificationsPage"));

export default function SettingsNotificationsPage() {
  return (
    <ClientPage permission={PERMISSIONS.SettingsView}>
      <NotificationsPage />
    </ClientPage>
  );
}

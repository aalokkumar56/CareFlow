"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";

const NotificationPreferencesPage = loadView(() => import("@/views/NotificationPreferencesPage"));

export default function NotificationPreferencesRoutePage() {
  return (
    <ClientPage>
      <NotificationPreferencesPage />
    </ClientPage>
  );
}

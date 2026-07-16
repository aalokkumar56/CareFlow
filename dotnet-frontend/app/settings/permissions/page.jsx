"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const PermissionsPage = loadView(() => import("@/views/settings/PermissionsPage"));

export default function SettingsPermissionsPage() {
  return (
    <ClientPage permission={PERMISSIONS.UserView}>
      <PermissionsPage />
    </ClientPage>
  );
}

"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const RolesPage = loadView(() => import("@/views/settings/RolesPage"));

export default function SettingsRolesPage() {
  return (
    <ClientPage permission={PERMISSIONS.UserView}>
      <RolesPage />
    </ClientPage>
  );
}

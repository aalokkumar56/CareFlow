"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const UsersPage = loadView(() => import("@/views/settings/UsersPage"));

export default function SettingsUsersPage() {
  return (
    <ClientPage permission={PERMISSIONS.UserView}>
      <UsersPage />
    </ClientPage>
  );
}

import React from "react";
import RequirePermission from "@/components/RequirePermission";
import SettingsLayout from "@/components/settings/SettingsLayout";
import SettingsNav from "@/components/settings/SettingsNav";
import RolesPermissionsPanel from "@/components/settings/RolesPermissionsPanel";
import { PERMISSIONS } from "@/lib/permissions";

const RolesPage = () => (
  <RequirePermission permission={PERMISSIONS.UserView}>
    <SettingsLayout title="Roles" sidebar={<SettingsNav />}>
      <div className="flex-1 min-w-0 min-h-0 overflow-hidden">
        <RolesPermissionsPanel fillHeight />
      </div>
    </SettingsLayout>
  </RequirePermission>
);

export default RolesPage;

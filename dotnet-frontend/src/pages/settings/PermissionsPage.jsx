import React from "react";
import RequirePermission from "@/components/RequirePermission";
import SettingsLayout from "@/components/settings/SettingsLayout";
import SettingsNav from "@/components/settings/SettingsNav";
import PermissionsCatalogPanel from "@/components/settings/PermissionsCatalogPanel";
import { PERMISSIONS } from "@/lib/permissions";

const PermissionsPage = () => (
  <RequirePermission permission={PERMISSIONS.UserView}>
    <SettingsLayout title="Permissions" sidebar={<SettingsNav />}>
      <div className="flex-1 min-w-0 min-h-0 overflow-hidden">
        <PermissionsCatalogPanel fillHeight />
      </div>
    </SettingsLayout>
  </RequirePermission>
);

export default PermissionsPage;

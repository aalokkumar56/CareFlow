import React from "react";
import RequirePermission from "@/components/RequirePermission";
import SettingsLayout from "@/components/settings/SettingsLayout";
import SettingsNav from "@/components/settings/SettingsNav";
import DepartmentsPanel from "@/components/settings/DepartmentsPanel";
import { PERMISSIONS } from "@/lib/permissions";

const HospitalPage = () => (
  <RequirePermission permission={PERMISSIONS.SettingsView}>
    <SettingsLayout title="Hospital" sidebar={<SettingsNav />}>
      <div className="flex-1 min-w-0 min-h-0 overflow-auto scrollbar-thin">
        <DepartmentsPanel />
      </div>
    </SettingsLayout>
  </RequirePermission>
);

export default HospitalPage;

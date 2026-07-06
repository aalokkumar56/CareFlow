import React from "react";
import RequirePermission from "@/components/RequirePermission";
import SettingsLayout from "@/components/settings/SettingsLayout";
import SettingsNav from "@/components/settings/SettingsNav";
import IntegrationsPanel from "@/components/settings/IntegrationsPanel";
import { PERMISSIONS } from "@/lib/permissions";

const IntegrationsPage = () => (
  <RequirePermission permission={PERMISSIONS.SettingsView}>
    <SettingsLayout title="Integrations" sidebar={<SettingsNav />}>
      <div className="flex-1 min-w-0 min-h-0 overflow-hidden">
        <IntegrationsPanel fillHeight />
      </div>
    </SettingsLayout>
  </RequirePermission>
);

export default IntegrationsPage;

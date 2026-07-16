import React from "react";
import RequirePermission from "@/components/RequirePermission";
import SettingsLayout from "@/components/settings/SettingsLayout";
import SettingsNav from "@/components/settings/SettingsNav";
import GlassCard from "@/components/glass/GlassCard";
import NotificationPreferencesContent from "@/components/notifications/NotificationPreferencesContent";
import { PERMISSIONS } from "@/lib/permissions";

const NotificationsPage = () => (
  <RequirePermission permission={PERMISSIONS.SettingsView}>
    <SettingsLayout title="Notifications" sidebar={<SettingsNav />}>
      <div className="flex-1 min-w-0 min-h-0 flex flex-col gap-2 overflow-hidden">
        <GlassCard padding={false} className="flex-1 min-h-0 overflow-hidden flex flex-col w-full">
          <div className="flex-1 min-h-0 overflow-hidden p-4 sm:p-6 flex flex-col">
            <NotificationPreferencesContent />
          </div>
        </GlassCard>
      </div>
    </SettingsLayout>
  </RequirePermission>
);

export default NotificationsPage;

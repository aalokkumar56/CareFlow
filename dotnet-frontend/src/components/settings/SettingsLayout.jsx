import React from "react";
import AppShell from "@/components/layout/AppShell";
import PageSideLayout from "@/components/layout/PageSideLayout";

const SettingsLayout = ({ sidebar, children, title = "Settings" }) => (
  <AppShell
    title={title}
    hideHeaderSearch
    hideHospitalBadge
    showDate={false}
    scrollable={false}
    compactFooter
    wide
  >
    <PageSideLayout sidebar={sidebar} className="flex-1 min-h-0 h-full">
      {children}
    </PageSideLayout>
  </AppShell>
);

export default SettingsLayout;

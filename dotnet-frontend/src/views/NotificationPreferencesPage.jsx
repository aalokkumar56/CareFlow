import React from "react";
import { Link } from "@/lib/navigation";
import AppShell from "@/components/layout/AppShell";
import PageContent from "@/components/glass/PageContent";
import GlassCard from "@/components/glass/GlassCard";
import NotificationPreferencesContent from "@/components/notifications/NotificationPreferencesContent";
import { Bell, ArrowLeft } from "@phosphor-icons/react";

const NotificationPreferencesPage = () => (
  <AppShell>
    <PageContent>
      <GlassCard padding={false} className="flex flex-col w-full max-w-4xl mx-auto overflow-hidden">
        <div className="shrink-0 p-4 sm:p-6 pb-4">
          <Link
            to="/"
            className="inline-flex items-center gap-1 text-xs text-text-muted hover:text-[#4338CA] mb-3"
          >
            <ArrowLeft className="w-3.5 h-3.5" />
            Back
          </Link>
          <div className="flex items-center gap-2">
            <Bell className="w-5 h-5 text-[#4338CA]" />
            <h1
              data-testid="notification-preferences-title"
              className="font-heading text-xl font-semibold text-[#022C22]"
            >
              Notification preferences
            </h1>
          </div>
        </div>
        <div className="flex-1 min-h-0 overflow-hidden px-4 sm:px-6 pb-6 flex flex-col">
          <NotificationPreferencesContent showRoleDefaults={false} />
        </div>
      </GlassCard>
    </PageContent>
  </AppShell>
);

export default NotificationPreferencesPage;

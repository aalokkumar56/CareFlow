"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const EmailInbox = loadView(() => import("@/views/EmailInbox"));

export default function EmailInboxPage() {
  return (
    <ClientPage permission={PERMISSIONS.ConversationView}>
      <EmailInbox />
    </ClientPage>
  );
}

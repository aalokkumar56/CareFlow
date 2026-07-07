"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const Inbox = loadView(() => import("@/views/Inbox"));

export default function InboxPage() {
  return (
    <ClientPage permission={PERMISSIONS.ConversationView}>
      <Inbox />
    </ClientPage>
  );
}

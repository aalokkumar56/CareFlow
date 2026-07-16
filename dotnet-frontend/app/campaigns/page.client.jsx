"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const Campaigns = loadView(() => import("@/views/Campaigns"));

export default function CampaignsPage() {
  return (
    <ClientPage permission={PERMISSIONS.CampaignView}>
      <Campaigns />
    </ClientPage>
  );
}

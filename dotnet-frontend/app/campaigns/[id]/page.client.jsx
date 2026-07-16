"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const CampaignDetail = loadView(() => import("@/views/CampaignDetail"));

export default function CampaignDetailPage() {
  return (
    <ClientPage permission={PERMISSIONS.CampaignView}>
      <CampaignDetail />
    </ClientPage>
  );
}

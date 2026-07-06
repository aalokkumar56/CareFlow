"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const DoctorDetail = loadView(() => import("@/views/DoctorDetail"));

export default function DoctorDetailPage() {
  return (
    <ClientPage permission={PERMISSIONS.ReferralView}>
      <DoctorDetail />
    </ClientPage>
  );
}

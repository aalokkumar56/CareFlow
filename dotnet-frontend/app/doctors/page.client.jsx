"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const Doctors = loadView(() => import("@/views/Doctors"));

export default function DoctorsPage() {
  return (
    <ClientPage permission={PERMISSIONS.ReferralView}>
      <Doctors />
    </ClientPage>
  );
}

"use client";

import ClientPage from "@/components/ClientPage";
import { loadView } from "@/components/loadView";
import { PERMISSIONS } from "@/lib/permissions";

const Staff = loadView(() => import("@/views/Staff"));

export default function StaffPage() {
  return (
    <ClientPage anyOf={[PERMISSIONS.StaffView, PERMISSIONS.ClinicalView]}>
      <Staff />
    </ClientPage>
  );
}

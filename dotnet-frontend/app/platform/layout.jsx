"use client";

import { usePathname } from "next/navigation";
import PlatformShell from "@/components/platform/PlatformShell";

export default function PlatformLayout({ children }) {
  const pathname = usePathname();
  const isLogin = pathname === "/platform/login";

  if (isLogin) {
    return children;
  }

  return <PlatformShell>{children}</PlatformShell>;
}

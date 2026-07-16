"use client";

import React, { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import CureFlowMark from "@/components/brand/CureFlowMark";
import { isPlatformAuthenticated, platformLogout } from "@/lib/platformAuth";
import { Button } from "@/components/ui/button";
import { Link } from "@/lib/navigation";
import { SignOut, Buildings } from "@phosphor-icons/react";
import { cn } from "@/lib/utils";

const PlatformShell = ({ children }) => {
  const pathname = usePathname();
  const router = useRouter();
  const [ready, setReady] = useState(false);
  const authenticated = isPlatformAuthenticated();

  useEffect(() => {
    setReady(true);
    if (!isPlatformAuthenticated()) {
      router.replace("/platform/login");
    }
  }, [pathname, router]);

  if (!ready) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <p className="text-sm text-muted-foreground">Loading...</p>
      </div>
    );
  }

  if (!authenticated) {
    return null;
  }

  return (
    <div className="min-h-screen bg-background flex flex-col" data-testid="platform-shell">
      <header className="border-b border-border/60 bg-white/40 backdrop-blur-md sticky top-0 z-40">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 h-14 flex items-center justify-between gap-4">
          <div className="flex items-center gap-4">
            <Link href="/platform/tenants" className="shrink-0">
              <CureFlowMark variant="compact" />
            </Link>
            <nav className="hidden sm:flex items-center gap-1">
              <Link
                href="/platform/tenants"
                className={cn(
                  "flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-medium transition-colors",
                  pathname?.startsWith("/platform/tenants")
                    ? "bg-primary/10 text-primary"
                    : "text-muted-foreground hover:text-foreground hover:bg-muted/50",
                )}
                data-testid="platform-nav-tenants"
              >
                <Buildings className="w-4 h-4" />
                Tenants
              </Link>
            </nav>
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={platformLogout}
            data-testid="platform-logout"
          >
            <SignOut className="w-4 h-4 mr-1" />
            Sign out
          </Button>
        </div>
      </header>
      <main className="flex-1">{children}</main>
    </div>
  );
};

export default PlatformShell;

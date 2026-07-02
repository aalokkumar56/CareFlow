import React, { useState } from "react";
import Sidebar, { SidebarNav, SidebarSettingsLink } from "./Sidebar";
import CommandPalette from "./CommandPalette";
import { MagnifyingGlass, List, CalendarBlank, Hospital, CaretDown } from "@phosphor-icons/react";
import { Toaster } from "@/components/ui/sonner";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { useAuth } from "@/lib/auth";
import { SignOut } from "@phosphor-icons/react";
import { formatRole } from "@/lib/permissions";
import { useHospitalProfile } from "@/hooks/useHospitalProfile";
import NotificationDropdown from "@/components/notifications/NotificationDropdown";
import { cn } from "@/lib/utils";

const AppShell = ({
  title,
  subtitle,
  greeting,
  breadcrumb,
  actions,
  children,
  wide,
  showDate = true,
  hideHeaderSearch = false,
  hideNotifications = false,
  hideHospitalBadge = false,
  headerTrailing = null,
  scrollable = true,
  compactFooter = false,
  variant = "default",
}) => {
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const { user, logout } = useAuth();
  const { hospitalName } = useHospitalProfile();

  const todayLabel = new Date().toLocaleDateString("en-IN", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

  const isDashboard = variant === "dashboard";

  return (
    <div className="flex h-screen mesh-gradient-bg overflow-hidden md:gap-3">
      <Sidebar />
      <main className={cn(
        "flex-1 flex flex-col overflow-hidden min-w-0 min-h-0 pr-2 sm:pr-3 pt-2 sm:pt-3 pb-2 sm:pb-3 mb-20 lg:mb-0",
      )}>
        <header className={cn(
          "shrink-0",
          isDashboard
            ? "md:hidden px-4 pt-2 pb-1"
            : "px-3 sm:px-5 lg:px-6 glass-header mx-0 py-2.5 sm:py-4 flex flex-nowrap items-center gap-2 sm:gap-3 justify-between",
        )}>
          {isDashboard && (
            <button
              type="button"
              data-testid="mobile-nav-toggle"
              onClick={() => setMobileNavOpen(true)}
              className="p-2 rounded-full glass-input hover:bg-white"
              aria-label="Open menu"
            >
              <List weight="bold" className="w-5 h-5 text-[#4338CA]" />
            </button>
          )}

          {!isDashboard && (
          <>
          <div className="flex items-center gap-3 min-w-0 flex-1">
            <button
              type="button"
              data-testid="mobile-nav-toggle"
              onClick={() => setMobileNavOpen(true)}
              className="md:hidden min-h-[44px] min-w-[44px] p-2 rounded-full glass-input hover:bg-white flex items-center justify-center relative z-20 shrink-0"
              aria-label="Open menu"
            >
              <List weight="bold" className="w-5 h-5 text-[#022C22]" />
            </button>
            <div className="min-w-0">
              {breadcrumb ? (
                <div data-testid="page-breadcrumb">{breadcrumb}</div>
              ) : greeting ? (
                <>
                  <h1 className="font-heading text-lg sm:text-[22px] font-semibold tracking-tight text-[#022C22] truncate" data-testid="page-title">
                    {greeting}
                  </h1>
                  {subtitle && (
                    <p className={cn(
                      "text-[12px] sm:text-[13px] text-text-secondary mt-0.5 truncate",
                      compactFooter && "hidden sm:block",
                    )}>{subtitle}</p>
                  )}
                </>
              ) : (
                <>
                  {title && (
                    <h1 className="font-heading text-lg sm:text-[20px] font-semibold tracking-tight text-[#022C22] truncate" data-testid="page-title">
                      {title}
                    </h1>
                  )}
                  {subtitle && (
                    <p className={cn(
                      "text-[12px] sm:text-[13px] text-text-secondary mt-0.5 truncate",
                      compactFooter && "hidden sm:block",
                    )}>{subtitle}</p>
                  )}
                </>
              )}
            </div>
          </div>
          </>
          )}

          {!isDashboard && (
          <>
          <div className="flex items-center gap-1.5 sm:gap-3 shrink-0 justify-end">
            {!hideHeaderSearch && (
            <button
              data-testid="open-command-palette"
              onClick={() => setPaletteOpen(true)}
              aria-label="Open command palette"
              className="flex items-center justify-center gap-2.5 text-[14px] text-[#64748B] glass-input hover:bg-white/50 transition-colors min-h-[44px] min-w-[44px] p-2.5 md:px-4 md:py-2.5 md:min-w-[200px] lg:min-w-[280px] relative z-10"
            >
              <MagnifyingGlass weight="regular" className="w-[18px] h-[18px] shrink-0 text-[#94A3B8]" />
              <span className="flex-1 text-left truncate hidden md:inline text-[#94A3B8]">Search or jump to...</span>
              <kbd className="font-mono text-[11px] bg-white/60 text-[#94A3B8] px-2 py-0.5 rounded-md hidden lg:inline">⌘K</kbd>
            </button>
            )}

            {headerTrailing}

            {!hideNotifications && <NotificationDropdown />}

            {showDate && (
              <div className="hidden sm:flex items-center gap-2 px-4 py-2.5 rounded-full glass-input text-[14px] text-[#1E293B] font-medium">
                <CalendarBlank weight="regular" className="w-[18px] h-[18px] text-[#64748B]" />
                {todayLabel}
                <CaretDown weight="bold" className="w-3 h-3 text-[#94A3B8]" />
              </div>
            )}

            {!hideHospitalBadge && (
            <div
              data-testid="hospital-name-badge"
              className="flex items-center gap-2 px-3 py-2 rounded-full bg-white/40 border border-white/50 text-[#0F172A]"
            >
              <Hospital weight="duotone" className="w-4 h-4 shrink-0 text-[#6366F1]" />
              <span className="text-[12px] font-semibold truncate max-w-[200px]">{hospitalName}</span>
            </div>
            )}

            {actions}
          </div>
          </>
          )}
        </header>

        <div className={cn(
          "flex-1 min-h-0 flex flex-col",
          (isDashboard || !scrollable) ? "overflow-hidden" : "overflow-y-auto scrollbar-thin",
        )}>
          {children}
        </div>
      </main>

      <Sheet open={mobileNavOpen} onOpenChange={setMobileNavOpen}>
        <SheetContent side="left" className="w-[280px] p-0 flex flex-col glass-panel border-white/60">
          <SheetHeader className="px-5 py-4 border-b border-white/50 text-left">
            <SheetTitle className="font-heading text-[#022C22]">CureFlow</SheetTitle>
          </SheetHeader>
          <SidebarNav
            onNavigate={() => setMobileNavOpen(false)}
            className="flex-1 min-h-0 overflow-y-auto"
          />
          <SidebarSettingsLink onNavigate={() => setMobileNavOpen(false)} />
          <div className="border-t border-white/50 px-3 py-3 shrink-0">
            <div className="flex items-center gap-3 px-2 py-2">
              <div className="w-8 h-8 rounded-full bg-[#064E3B] text-white flex items-center justify-center text-xs font-semibold">
                {user?.name?.[0]?.toUpperCase() || "U"}
              </div>
              <div className="flex-1 min-w-0">
                <div className="text-sm font-medium text-[#022C22] truncate">{user?.name}</div>
                <div className="text-[10px] uppercase tracking-wider text-text-muted">{formatRole(user?.role)}</div>
              </div>
              <button type="button" onClick={logout} className="text-text-muted hover:text-[#991B1B]" title="Logout" aria-label="Logout">
                <SignOut weight="regular" className="w-4 h-4" />
              </button>
            </div>
          </div>
        </SheetContent>
      </Sheet>

      <CommandPalette open={paletteOpen} setOpen={setPaletteOpen} />
      <Toaster position="top-right" />
    </div>
  );
};

export default AppShell;

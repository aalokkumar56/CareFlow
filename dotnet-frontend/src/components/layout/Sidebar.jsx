import React from "react";
import { NavLink } from "@/lib/navigation";
import {
  House, UsersThree, CalendarBlank,
  ListChecks, GearSix, Sparkle, ChartBar,
  Stethoscope, Megaphone, UserCircle, SignOut, EnvelopeSimple,
  Hospital, CaretDoubleLeft, CaretDoubleRight,
} from "@phosphor-icons/react";
import SidePanel from "@/components/layout/SidePanel";
import { useAuth } from "@/lib/auth";
import usePermissions from "@/hooks/usePermissions";
import { useHospitalProfile } from "@/hooks/useHospitalProfile";
import { PERMISSIONS, formatRole } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";

export const navItems = [
  { to: "/", label: "Dashboard", testId: "nav-dashboard", icon: House, end: true, permission: PERMISSIONS.DashboardView },
  { to: "/appointments", label: "Appointments", testId: "nav-appointments", icon: CalendarBlank, permission: PERMISSIONS.AppointmentView },
  { to: "/patients", label: "Patients", testId: "nav-patients", icon: UsersThree, permission: PERMISSIONS.PatientView },
  { to: "/inbox", label: "WhatsApp Inbox", testId: "nav-whatsapp-inbox", icon: Sparkle, permission: PERMISSIONS.ConversationView },
  { to: "/email-inbox", label: "Email Inbox", testId: "nav-email-inbox", icon: EnvelopeSimple, permission: PERMISSIONS.ConversationView },
  { to: "/campaigns", label: "Campaigns", testId: "nav-campaigns", icon: Megaphone, permission: PERMISSIONS.CampaignView },
  { to: "/missed-revenue", label: "Analytics", testId: "nav-analytics", icon: ChartBar, permission: PERMISSIONS.DashboardView },
  { to: "/tasks", label: "Follow-ups", testId: "nav-follow-ups", icon: ListChecks, permission: PERMISSIONS.DashboardView },
  { to: "/doctors", label: "Referral CRM", testId: "nav-referral-crm", icon: Stethoscope, permission: PERMISSIONS.ReferralView },
  { to: "/staff", label: "Hospital Staff", testId: "nav-hospital-staff", icon: UserCircle, anyPermission: [PERMISSIONS.StaffView, PERMISSIONS.ClinicalView] },
];

const navLinkClass = (isActive, pending = false, collapsed = false) =>
  cn(
    "flex items-center min-h-[44px] rounded-xl text-ui-base transition-colors duration-200",
    collapsed ? "justify-center px-2 gap-0" : "gap-3 px-3.5",
    pending && "opacity-70",
    isActive
      ? "sidebar-nav-active text-[#064E3B]"
      : "text-[#4B5563] hover:bg-white/50 hover:text-[#022C22]",
  );

const NavItemTooltip = ({ collapsed, label, children }) => {
  if (!collapsed) return children;

  return (
    <Tooltip delayDuration={0}>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent side="right" className="bg-[#022C22] text-white border-none">
        {label}
      </TooltipContent>
    </Tooltip>
  );
};

export const SidebarNav = ({ onNavigate, className, includeSettings = false, collapsed = false }) => {
  const { can, canAny } = usePermissions();
  const visibleNav = navItems.filter((item) =>
    item.anyPermission ? canAny(item.anyPermission) : can(item.permission),
  );
  const showSettings = includeSettings && can(PERMISSIONS.SettingsView);

  return (
    <nav className={cn("flex-1 min-h-0 px-2.5 py-2 space-y-1 overflow-y-auto scrollbar-thin", className)}>
      {visibleNav.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.end}
          onClick={onNavigate}
          data-testid={item.testId || `nav-${item.label.toLowerCase().replace(/\s+/g, "-")}`}
        >
          {({ isActive, pending }) => (
            <NavItemTooltip collapsed={collapsed} label={item.label}>
              <div className={navLinkClass(isActive, pending, collapsed)}>
                <item.icon weight={isActive ? "fill" : "regular"} className="w-[18px] h-[18px] shrink-0" />
                {!collapsed && (
                  <>
                    <span className="flex-1 truncate">{item.label}</span>
                    {item.badge && (
                      <span className="text-[9px] uppercase tracking-wide px-1.5 py-0.5 rounded-md bg-violet-500 text-white font-bold shrink-0">
                        {item.badge}
                      </span>
                    )}
                  </>
                )}
              </div>
            </NavItemTooltip>
          )}
        </NavLink>
      ))}

      {showSettings && (
        <NavLink to="/settings" onClick={onNavigate} data-testid="nav-settings">
          {({ isActive, pending }) => (
            <NavItemTooltip collapsed={collapsed} label="Settings">
              <div className={cn(navLinkClass(isActive, pending, collapsed), "mt-1")}>
                <GearSix weight={isActive ? "fill" : "regular"} className="w-[18px] h-[18px]" />
                {!collapsed && <span>Settings</span>}
              </div>
            </NavItemTooltip>
          )}
        </NavLink>
      )}
    </nav>
  );
};

export const SidebarSettingsLink = ({ onNavigate, collapsed = false }) => {
  const { can } = usePermissions();
  if (!can(PERMISSIONS.SettingsView)) return null;

  return (
    <div className="shrink-0 px-2.5 pb-1 border-t border-white/40 pt-2">
      <NavLink to="/settings" onClick={onNavigate} data-testid="nav-settings">
        {({ isActive, pending }) => (
          <NavItemTooltip collapsed={collapsed} label="Settings">
            <div className={navLinkClass(isActive, pending, collapsed)}>
              <GearSix weight={isActive ? "fill" : "regular"} className="w-[18px] h-[18px]" />
              {!collapsed && <span>Settings</span>}
            </div>
          </NavItemTooltip>
        )}
      </NavLink>
    </div>
  );
};

const SidebarBrand = ({ hospitalName, collapsed, onToggleCollapse }) => {
  const initial = hospitalName?.trim()?.[0]?.toUpperCase() || "H";

  if (collapsed) {
    return (
      <div className="flex flex-col items-center gap-2 shrink-0">
        <Tooltip delayDuration={0}>
          <TooltipTrigger asChild>
            <div
              className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500 to-teal-500 text-white flex items-center justify-center ring-2 ring-white/50 shrink-0"
              data-testid="sidebar-brand"
            >
              <span className="font-heading text-sm font-bold">{initial}</span>
            </div>
          </TooltipTrigger>
          <TooltipContent side="right" className="bg-[#022C22] text-white border-none max-w-[220px]">
            {hospitalName}
          </TooltipContent>
        </Tooltip>
        <button
          type="button"
          data-testid="sidebar-collapse-toggle"
          onClick={onToggleCollapse}
          className="min-h-[36px] min-w-[36px] rounded-lg flex items-center justify-center text-slate-500 hover:bg-white/40 hover:text-[#022C22] transition-colors"
          aria-label="Expand sidebar"
          title="Expand sidebar"
        >
          <CaretDoubleRight weight="bold" className="w-4 h-4" />
        </button>
      </div>
    );
  }

  return (
    <div className="flex items-start gap-2.5 min-w-0" data-testid="sidebar-brand">
      <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500 to-teal-500 text-white flex items-center justify-center ring-2 ring-white/50 shrink-0">
        <Hospital weight="duotone" className="w-5 h-5" />
      </div>
      <div className="flex-1 min-w-0 pt-0.5">
        <p
          className="font-heading text-[15px] font-semibold text-[#022C22] truncate leading-tight"
          data-testid="sidebar-hospital-name"
          title={hospitalName}
        >
          {hospitalName}
        </p>
        <p className="text-[10px] text-slate-500 mt-0.5">Hospital workspace</p>
      </div>
      <button
        type="button"
        data-testid="sidebar-collapse-toggle"
        onClick={onToggleCollapse}
        className="min-h-[32px] min-w-[32px] rounded-lg flex items-center justify-center text-slate-400 hover:bg-white/40 hover:text-[#022C22] transition-colors shrink-0 -mr-1"
        aria-label="Collapse sidebar"
        title="Collapse sidebar"
      >
        <CaretDoubleLeft weight="bold" className="w-4 h-4" />
      </button>
    </div>
  );
};

const Sidebar = ({ className = "", collapsed = false, onToggleCollapse }) => {
  const { user, tenant, logout } = useAuth();
  const { hospitalName: profileName } = useHospitalProfile();
  const hospitalName = tenant?.name || profileName;

  return (
    <TooltipProvider>
      <SidePanel
        testId="app-sidebar"
        collapsed={collapsed}
        className={cn(
          "pl-2 md:pl-3 py-3 md:py-4 h-full min-h-0 transition-[width] duration-300 ease-in-out",
          className,
        )}
      >
        <div className={cn("shrink-0", collapsed ? "px-1 pt-3 pb-2" : "px-3 pt-4 pb-3")}>
          <SidebarBrand
            hospitalName={hospitalName}
            collapsed={collapsed}
            onToggleCollapse={onToggleCollapse}
          />
        </div>

        <SidebarNav collapsed={collapsed} />

        <SidebarSettingsLink collapsed={collapsed} />

        <div className={cn("border-t border-white/50 shrink-0", collapsed ? "px-1.5 py-2" : "px-3 py-3")}>
          <div
            className={cn(
              "flex items-center rounded-xl bg-white/30 hover:bg-white/40 transition-colors",
              collapsed ? "justify-center p-2" : "gap-3 px-3 py-2.5",
            )}
          >
            <Tooltip delayDuration={0}>
              <TooltipTrigger asChild>
                <div className="w-9 h-9 rounded-full bg-gradient-to-br from-indigo-400 to-teal-400 text-white flex items-center justify-center text-sm font-semibold shrink-0 ring-2 ring-white/60">
                  {user?.name?.[0]?.toUpperCase() || "U"}
                </div>
              </TooltipTrigger>
              {collapsed && (
                <TooltipContent side="right" className="bg-[#022C22] text-white border-none">
                  <div className="font-medium">{user?.name}</div>
                  <div className="text-white/70 text-[10px]">{formatRole(user?.role)}</div>
                </TooltipContent>
              )}
            </Tooltip>
            {!collapsed && (
              <>
                <div className="flex-1 min-w-0">
                  <div className="text-[13px] font-semibold text-slate-900 truncate" data-testid="sidebar-user-name">
                    {user?.name}
                  </div>
                  <div className="text-[11px] text-slate-500 truncate">
                    {formatRole(user?.role)}
                  </div>
                </div>
                <button
                  type="button"
                  data-testid="logout-btn"
                  onClick={logout}
                  className="min-h-[44px] min-w-[44px] rounded-xl flex items-center justify-center text-slate-400 hover:text-red-700 transition-colors shrink-0"
                  title="Logout"
                  aria-label="Logout"
                >
                  <SignOut weight="bold" className="w-4 h-4" />
                </button>
              </>
            )}
          </div>
          {collapsed && (
            <button
              type="button"
              data-testid="logout-btn"
              onClick={logout}
              className="mt-1.5 w-full min-h-[36px] rounded-lg flex items-center justify-center text-slate-400 hover:bg-white/40 hover:text-red-700 transition-colors"
              title="Logout"
              aria-label="Logout"
            >
              <SignOut weight="bold" className="w-4 h-4" />
            </button>
          )}
        </div>
      </SidePanel>
    </TooltipProvider>
  );
};

export default Sidebar;

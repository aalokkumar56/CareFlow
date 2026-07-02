import React from "react";
import { NavLink } from "react-router-dom";
import {
  House, UsersThree, CalendarBlank,
  ListChecks, GearSix, Sparkle, ChartBar,
  Stethoscope, Megaphone, UserCircle, SignOut, EnvelopeSimple,
} from "@phosphor-icons/react";
import SidePanel from "@/components/layout/SidePanel";
import { useAuth } from "@/lib/auth";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS, formatRole } from "@/lib/permissions";
import { cn } from "@/lib/utils";

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

const navLinkClass = (isActive) =>
  cn(
    "flex items-center gap-3 px-3.5 min-h-[44px] rounded-xl text-ui-base transition-colors duration-200",
    isActive
      ? "sidebar-nav-active text-[#064E3B]"
      : "text-[#4B5563] hover:bg-white/50 hover:text-[#022C22]",
  );

export const SidebarNav = ({ onNavigate, className, includeSettings = false }) => {
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
          {({ isActive }) => (
            <div className={navLinkClass(isActive)}>
              <item.icon weight={isActive ? "fill" : "regular"} className="w-[18px] h-[18px] shrink-0" />
              <span className="flex-1 truncate">{item.label}</span>
              {item.badge && (
                <span className="text-[9px] uppercase tracking-wide px-1.5 py-0.5 rounded-md bg-violet-500 text-white font-bold shrink-0">
                  {item.badge}
                </span>
              )}
            </div>
          )}
        </NavLink>
      ))}

      {showSettings && (
        <NavLink to="/settings" onClick={onNavigate} data-testid="nav-settings">
          {({ isActive }) => (
            <div className={cn(navLinkClass(isActive), "mt-1")}>
              <GearSix weight={isActive ? "fill" : "regular"} className="w-[18px] h-[18px]" />
              <span>Settings</span>
            </div>
          )}
        </NavLink>
      )}
    </nav>
  );
};

export const SidebarSettingsLink = ({ onNavigate }) => {
  const { can } = usePermissions();
  if (!can(PERMISSIONS.SettingsView)) return null;

  return (
    <div className="shrink-0 px-2.5 pb-1 border-t border-white/40 pt-2">
      <NavLink to="/settings" onClick={onNavigate} data-testid="nav-settings">
        {({ isActive }) => (
          <div className={navLinkClass(isActive)}>
            <GearSix weight={isActive ? "fill" : "regular"} className="w-[18px] h-[18px]" />
            <span>Settings</span>
          </div>
        )}
      </NavLink>
    </div>
  );
};

const Sidebar = ({ className = "" }) => {
  const { user, logout } = useAuth();

  return (
    <SidePanel
      testId="app-sidebar"
      className={cn(
        "pl-3 md:pl-4 py-3 md:py-4 h-full min-h-0",
        className,
      )}
    >
      <div className="px-4 pt-5 pb-4 shrink-0">
        <div className="flex items-center gap-2.5" data-testid="sidebar-brand">
          <img
            src="/design/cureflow-logo.svg"
            alt="CureFlow"
            className="h-9 w-auto"
            width="160"
            height="36"
          />
        </div>
      </div>

      <SidebarNav />

      <SidebarSettingsLink />

      <div className="border-t border-white/50 px-3 py-3 shrink-0">
        <div className="flex items-center gap-3 px-3 py-2.5 rounded-xl bg-white/30 hover:bg-white/40 transition-colors">
          <div className="w-9 h-9 rounded-full bg-gradient-to-br from-indigo-400 to-teal-400 text-white flex items-center justify-center text-sm font-semibold shrink-0 ring-2 ring-white/60">
            {user?.name?.[0]?.toUpperCase() || "U"}
          </div>
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
        </div>
      </div>
    </SidePanel>
  );
};

export default Sidebar;

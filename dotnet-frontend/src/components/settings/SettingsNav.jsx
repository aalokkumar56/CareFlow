import React from "react";
import { NavLink } from "@/lib/navigation";
import { Users, Shield, Key, ChatText, Plugs, Buildings, Bell } from "@phosphor-icons/react";
import SidePanel from "@/components/layout/SidePanel";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";
import { cn } from "@/lib/utils";

const items = [
  { to: "/settings/users", label: "Users", icon: Users, permission: PERMISSIONS.UserView },
  { to: "/settings/roles", label: "Roles", icon: Shield, permission: PERMISSIONS.UserView },
  { to: "/settings/permissions", label: "Permissions", icon: Key, permission: PERMISSIONS.UserView },
  { to: "/settings/templates", label: "Templates", icon: ChatText, permission: PERMISSIONS.SettingsView },
  { to: "/settings/notifications", label: "Notifications", icon: Bell, permission: PERMISSIONS.SettingsView },
  { to: "/settings/hospital", label: "Hospital", icon: Buildings, permission: PERMISSIONS.SettingsView },
  { to: "/settings/integrations", label: "Integrations", icon: Plugs, permission: PERMISSIONS.SettingsView },
];

const NavItem = ({ item }) => (
  <NavLink
    to={item.to}
    className={({ isActive }) =>
      cn(
        "relative flex items-center gap-2 px-2.5 py-2 rounded-lg text-ui-sm transition-colors whitespace-nowrap lg:whitespace-normal",
        isActive
          ? "bg-sky-50/90 text-[#4338CA] font-semibold border border-sky-200/50 shadow-sm"
          : "text-text-secondary hover:text-[#022C22] hover:bg-white/35 border border-transparent",
      )
    }
  >
    {({ isActive }) => (
      <>
        {isActive && (
          <span className="absolute left-0 top-2 bottom-2 w-[3px] rounded-full bg-[#4338CA]" aria-hidden />
        )}
        <item.icon weight={isActive ? "fill" : "regular"} className="w-[18px] h-[18px] shrink-0 ml-0.5" />
        {item.label}
      </>
    )}
  </NavLink>
);

const SettingsNav = () => {
  const { can } = usePermissions();
  const visible = items.filter((item) => can(item.permission));

  if (visible.length === 0) return null;

  return (
    <SidePanel
      size="sm"
      visible="always"
      testId="settings-nav"
      className="p-1.5 flex flex-row lg:flex-col gap-0.5 overflow-x-auto lg:overflow-visible h-auto lg:h-full"
    >
      {visible.map((item) => (
        <NavItem key={item.to} item={item} />
      ))}
    </SidePanel>
  );
};

export default SettingsNav;

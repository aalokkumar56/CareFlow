import React from "react";
import { Navigate } from "react-router-dom";
import usePermissions from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/lib/permissions";

const Settings = () => {
  const { can } = usePermissions();
  if (can(PERMISSIONS.UserView)) {
    return <Navigate to="/settings/roles" replace />;
  }
  return <Navigate to="/settings/integrations" replace />;
};

export default Settings;

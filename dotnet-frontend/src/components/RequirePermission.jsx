import React from "react";
import { Navigate } from "react-router-dom";
import usePermissions from "@/hooks/usePermissions";

const RequirePermission = ({ permission, anyOf, children, fallback = "/" }) => {
  const { can, canAny, user } = usePermissions();
  if (!user) return null;
  const allowed = anyOf?.length ? canAny(anyOf) : can(permission);
  if (!allowed) return <Navigate to={fallback} replace />;
  return children;
};

export default RequirePermission;

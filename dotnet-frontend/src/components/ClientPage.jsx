"use client";

import ProtectedRoute from "@/components/ProtectedRoute";
import RequirePermission from "@/components/RequirePermission";

const ClientPage = ({ children, permission, anyOf }) => (
  <ProtectedRoute>
    {permission || anyOf?.length ? (
      <RequirePermission permission={permission} anyOf={anyOf}>
        {children}
      </RequirePermission>
    ) : (
      children
    )}
  </ProtectedRoute>
);

export default ClientPage;

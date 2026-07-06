"use client";

import { useAuth } from "@/lib/auth";
import { Navigate } from "@/lib/navigation";

const ProtectedRoute = ({ children }) => {
  const { user, loading } = useAuth();
  if (loading && !user) {
    return (
      <div className="h-screen w-screen flex items-center justify-center bg-background">
        <div className="text-text-muted text-sm">Loading...</div>
      </div>
    );
  }
  if (!user) return <Navigate to="/login" replace />;
  return children;
};

export default ProtectedRoute;

import React from "react";
import "@/index.css";
import "@/App.css";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AuthProvider, useAuth } from "@/lib/auth";
import ErrorBoundary from "@/components/ErrorBoundary";
import Login from "@/pages/Login";
import Dashboard from "@/pages/Dashboard";
import Inbox from "@/pages/Inbox";
import EmailInbox from "@/pages/EmailInbox";
import Patients from "@/pages/Patients";
import PatientDetail from "@/pages/PatientDetail";
import Appointments from "@/pages/Appointments";
import Tasks from "@/pages/Tasks";
import MissedRevenue from "@/pages/MissedRevenue";
import Doctors from "@/pages/Doctors";
import Staff from "@/pages/Staff";
import DoctorDetail from "@/pages/DoctorDetail";
import Campaigns from "@/pages/Campaigns";
import CampaignDetail from "@/pages/CampaignDetail";
import Settings from "@/pages/Settings";
import RequirePermission from "@/components/RequirePermission";
import { PERMISSIONS } from "@/lib/permissions";
import UsersPage from "@/pages/settings/UsersPage";
import RolesPage from "@/pages/settings/RolesPage";
import PermissionsPage from "@/pages/settings/PermissionsPage";
import TemplatesPage from "@/pages/settings/TemplatesPage";
import IntegrationsPage from "@/pages/settings/IntegrationsPage";
import HospitalPage from "@/pages/settings/HospitalPage";
import NotificationsPage from "@/pages/settings/NotificationsPage";
import NotificationPreferencesPage from "@/pages/NotificationPreferencesPage";
import { Toaster } from "@/components/ui/sonner";
import GlobalLoader from "@/components/GlobalLoader";

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

function App() {
  return (
    <ErrorBoundary>
      <BrowserRouter>
        <AuthProvider>
          <GlobalLoader />
          <Toaster position="top-right" />
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route
              path="/"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.DashboardView}>
                    <Dashboard />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/inbox"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.ConversationView}>
                    <Inbox />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/email-inbox"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.ConversationView}>
                    <EmailInbox />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/patients"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.PatientView}>
                    <Patients />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/patients/:id"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.PatientView}>
                    <PatientDetail />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/appointments"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.AppointmentView}>
                    <Appointments />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/tasks"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.DashboardView}>
                    <Tasks />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/doctors"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.ReferralView}>
                    <Doctors />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/staff"
              element={
                <ProtectedRoute>
                  <RequirePermission anyOf={[PERMISSIONS.StaffView, PERMISSIONS.ClinicalView]}>
                    <Staff />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/doctors/:id"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.ReferralView}>
                    <DoctorDetail />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/campaigns"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.CampaignView}>
                    <Campaigns />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/campaigns/:id"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.CampaignView}>
                    <CampaignDetail />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/missed-revenue"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.DashboardView}>
                    <MissedRevenue />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/settings"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.SettingsView}>
                    <Settings />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/settings/users"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.UserView}>
                    <UsersPage />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/settings/roles"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.UserView}>
                    <RolesPage />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/settings/permissions"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.UserView}>
                    <PermissionsPage />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/settings/templates"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.SettingsView}>
                    <TemplatesPage />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/notifications/preferences"
              element={
                <ProtectedRoute>
                  <NotificationPreferencesPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/settings/notifications"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.SettingsView}>
                    <NotificationsPage />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/settings/hospital"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.SettingsView}>
                    <HospitalPage />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route
              path="/settings/integrations"
              element={
                <ProtectedRoute>
                  <RequirePermission permission={PERMISSIONS.SettingsView}>
                    <IntegrationsPage />
                  </RequirePermission>
                </ProtectedRoute>
              }
            />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </ErrorBoundary>
  );
}

export default App;

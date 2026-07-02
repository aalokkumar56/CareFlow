namespace CureFlow.Application.Common;

/// <summary>Canonical permission codes used for RBAC policies and JWT claims.</summary>
public static class CureFlowPermissions
{
    public const string ClaimType = "permission";

    // Patient
    public const string PatientView = "Patient.View";
    public const string PatientCreate = "Patient.Create";
    public const string PatientEdit = "Patient.Edit";
    public const string PatientDelete = "Patient.Delete";

    // Appointment
    public const string AppointmentView = "Appointment.View";
    public const string AppointmentCreate = "Appointment.Create";
    public const string AppointmentEdit = "Appointment.Edit";
    public const string AppointmentDelete = "Appointment.Delete";

    // Billing
    public const string BillingView = "Billing.View";
    public const string BillingCreate = "Billing.Create";
    public const string BillingEdit = "Billing.Edit";
    public const string BillingDelete = "Billing.Delete";

    // User
    public const string UserView = "User.View";
    public const string UserCreate = "User.Create";
    public const string UserEdit = "User.Edit";
    public const string UserDelete = "User.Delete";

    // Staff
    public const string StaffView = "Staff.View";
    public const string StaffCreate = "Staff.Create";
    public const string StaffEdit = "Staff.Edit";
    public const string StaffDelete = "Staff.Delete";

    // WhatsApp
    public const string WhatsAppView = "WhatsApp.View";
    public const string WhatsAppSend = "WhatsApp.Send";
    public const string WhatsAppManage = "WhatsApp.Manage";

    // Conversation
    public const string ConversationView = "Conversation.View";
    public const string ConversationManage = "Conversation.Manage";

    // Campaign
    public const string CampaignView = "Campaign.View";
    public const string CampaignManage = "Campaign.Manage";

    // Clinical
    public const string ClinicalView = "Clinical.View";
    public const string ClinicalEdit = "Clinical.Edit";

    // Dashboard & audit
    public const string DashboardView = "Dashboard.View";
    public const string AuditView = "Audit.View";

    // Settings & referral
    public const string SettingsView = "Settings.View";
    public const string SettingsEdit = "Settings.Edit";
    public const string ReferralView = "Referral.View";
    public const string ReferralManage = "Referral.Manage";

    public static readonly IReadOnlyList<string> All =
    [
        PatientView, PatientCreate, PatientEdit, PatientDelete,
        AppointmentView, AppointmentCreate, AppointmentEdit, AppointmentDelete,
        BillingView, BillingCreate, BillingEdit, BillingDelete,
        UserView, UserCreate, UserEdit, UserDelete,
        StaffView, StaffCreate, StaffEdit, StaffDelete,
        WhatsAppView, WhatsAppSend, WhatsAppManage,
        ConversationView, ConversationManage,
        CampaignView, CampaignManage,
        ClinicalView, ClinicalEdit,
        DashboardView, AuditView,
        SettingsView, SettingsEdit,
        ReferralView, ReferralManage,
    ];

    public static readonly IReadOnlyList<string> ViewOnly =
    [
        PatientView, AppointmentView, BillingView, UserView,
        WhatsAppView, ConversationView, CampaignView, ClinicalView,
        DashboardView, AuditView, SettingsView, ReferralView, StaffView,
    ];

    public static string PolicyName(string permissionCode) => $"Permission:{permissionCode}";

    /// <summary>Maps legacy <see cref="Domain.Enums.UserRole"/> enum values to seeded role names.</summary>
    public static string MapLegacyRole(Domain.Enums.UserRole role) => role switch
    {
        Domain.Enums.UserRole.TenantOwner => RoleNames.SuperAdmin,
        Domain.Enums.UserRole.Admin => RoleNames.Admin,
        Domain.Enums.UserRole.Doctor => RoleNames.Doctor,
        Domain.Enums.UserRole.Reception => RoleNames.Receptionist,
        Domain.Enums.UserRole.Marketing => RoleNames.Marketing,
        Domain.Enums.UserRole.Nurse => RoleNames.Nurse,
        Domain.Enums.UserRole.Staff => RoleNames.Staff,
        _ => RoleNames.Staff,
    };
}

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Receptionist = "Receptionist";
    public const string Doctor = "Doctor";
    public const string Nurse = "Nurse";
    public const string Marketing = "Marketing";
    public const string Staff = "Staff";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All =
    [
        SuperAdmin, Admin, Receptionist, Doctor, Nurse, Marketing, Staff, Viewer,
    ];
}

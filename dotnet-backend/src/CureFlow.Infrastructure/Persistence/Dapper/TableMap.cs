using CureFlow.Domain.Common;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Entities.Lifestyle;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Domain.Entities.Saas;

namespace CureFlow.Infrastructure.Persistence.Dapper;

public static class TableMap
{
    private static readonly Dictionary<Type, string> Map = new()
    {
        [typeof(Tenant)] = "Tenants",
        [typeof(User)] = "Users",
        [typeof(Patient)] = "Patients",
        [typeof(Conversation)] = "Conversations",
        [typeof(Message)] = "Messages",
        [typeof(InternalNote)] = "InternalNotes",
        [typeof(Appointment)] = "Appointments",
        [typeof(TaskItem)] = "Tasks",
        [typeof(ReferringDoctor)] = "ReferringDoctors",
        [typeof(Referral)] = "Referrals",
        [typeof(Campaign)] = "Campaigns",
        [typeof(CampaignRecipient)] = "CampaignRecipients",
        [typeof(MarketingCalendarEvent)] = "MarketingCalendarEvents",
        [typeof(Allergy)] = "Allergies",
        [typeof(Prescription)] = "Prescriptions",
        [typeof(PrescriptionItem)] = "PrescriptionItems",
        [typeof(Injection)] = "Injections",
        [typeof(VitalSigns)] = "VitalSigns",
        [typeof(LabReport)] = "LabReports",
        [typeof(ClinicalNote)] = "ClinicalNotes",
        [typeof(MedicalHistoryItem)] = "MedicalHistory",
        [typeof(FamilyHistoryItem)] = "FamilyHistory",
        [typeof(Visit)] = "Visits",
        [typeof(StaffProfile)] = "StaffProfiles",
        [typeof(DoctorSchedule)] = "DoctorSchedules",
        [typeof(LifestyleProfile)] = "LifestyleProfiles",
        [typeof(HospitalProfile)] = "HospitalProfiles",
        [typeof(QuickTemplate)] = "Templates",
        [typeof(TemplatePlaceholder)] = "TemplatePlaceholders",
        [typeof(AuditLog)] = "AuditLogs",
        [typeof(WhatsAppSettings)] = "WhatsAppSettings",
        [typeof(SmsSettings)] = "SmsSettings",
        [typeof(EmailSettings)] = "EmailSettings",
        [typeof(EmailMessage)] = "EmailMessages",
        [typeof(IntegrationEvent)] = "IntegrationEvents",
        [typeof(WhatsAppTemplate)] = "WhatsAppTemplates",
        [typeof(WhatsAppGroup)] = "WhatsAppGroups",
        [typeof(WhatsAppCampaign)] = "WhatsAppCampaigns",
        [typeof(WhatsAppContact)] = "WhatsAppContacts",
        [typeof(WhatsAppMessage)] = "WhatsAppMessages",
        [typeof(PermissionGroup)] = "PermissionGroups",
        [typeof(Permission)] = "Permissions",
        [typeof(Role)] = "Roles",
        [typeof(RolePermission)] = "RolePermissions",
        [typeof(UserRoleAssignment)] = "UserRoleAssignments",
        [typeof(NotificationEvent)] = "NotificationEvents",
        [typeof(NotificationReceipt)] = "NotificationReceipts",
        [typeof(NotificationFeedCursor)] = "NotificationFeedCursors",
        [typeof(NotificationPreference)] = "NotificationPreferences",
        [typeof(RoleNotificationDefault)] = "RoleNotificationDefaults",
    };

    public static string GetTableName<T>() where T : BaseEntity => GetTableName(typeof(T));

    public static string GetTableName(Type type) =>
        Map.TryGetValue(type, out var name) ? name : type.Name + "s";
}

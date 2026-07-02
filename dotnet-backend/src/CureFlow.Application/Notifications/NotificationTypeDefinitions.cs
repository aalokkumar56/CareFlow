using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Notifications;

public sealed record NotificationTypeDefinition(
    string Code,
    string Category,
    IReadOnlyList<string> RequiredPermissions,
    NotificationSeverity DefaultSeverity,
    NotificationRecipientStrategy RecipientStrategy,
    bool DefaultEnabled,
    IReadOnlyDictionary<string, bool>? DefaultEnabledByRole = null);

public static class NotificationTypeDefinitions
{
    private static readonly Dictionary<string, NotificationTypeDefinition> ByCode = Build();

    public static IReadOnlyList<NotificationTypeDefinition> All => ByCode.Values.ToList();

    public static NotificationTypeDefinition Get(string code) =>
        ByCode.TryGetValue(code, out var def)
            ? def
            : throw new ArgumentException($"Unknown notification type: {code}", nameof(code));

    public static bool TryGet(string code, out NotificationTypeDefinition? definition) =>
        ByCode.TryGetValue(code, out definition);

    public static bool HasRequiredPermissions(IReadOnlyList<string> userPermissions, NotificationTypeDefinition definition) =>
        NotificationRbac.HasAllPermissions(userPermissions, definition.RequiredPermissions);

    public static bool IsEnabledForRole(string roleName, NotificationTypeDefinition definition)
    {
        if (definition.DefaultEnabledByRole != null
            && definition.DefaultEnabledByRole.TryGetValue(roleName, out var enabled))
            return enabled;
        return definition.DefaultEnabled;
    }

    private static Dictionary<string, NotificationTypeDefinition> Build()
    {
        var adminRoles = new Dictionary<string, bool>
        {
            ["SuperAdmin"] = true,
            ["Admin"] = true,
            ["Receptionist"] = true,
            ["Doctor"] = true,
            ["Nurse"] = true,
            ["Marketing"] = true,
            ["Staff"] = true,
            ["Billing"] = true,
        };

        return new Dictionary<string, NotificationTypeDefinition>(StringComparer.Ordinal)
        {
            [NotificationTypeCodes.WhatsappInboundMessage] = new(
                NotificationTypeCodes.WhatsappInboundMessage, "Inbox",
                [CureFlowPermissions.ConversationView],
                NotificationSeverity.Info,
                NotificationRecipientStrategy.AssignedStaff,
                true,
                new Dictionary<string, bool>(adminRoles)
                {
                    ["Nurse"] = false,
                    ["Marketing"] = false,
                    ["Billing"] = false,
                }),

            [NotificationTypeCodes.WhatsappLeadEscalation] = new(
                NotificationTypeCodes.WhatsappLeadEscalation, "Inbox",
                [CureFlowPermissions.ConversationManage],
                NotificationSeverity.Warning,
                NotificationRecipientStrategy.PermissionHolders,
                true,
                new Dictionary<string, bool>
                {
                    ["SuperAdmin"] = true,
                    ["Admin"] = true,
                    ["Receptionist"] = true,
                }),

            [NotificationTypeCodes.AppointmentCreated] = new(
                NotificationTypeCodes.AppointmentCreated, "Appointments",
                [CureFlowPermissions.AppointmentView],
                NotificationSeverity.Info,
                NotificationRecipientStrategy.AppointmentDoctor,
                true,
                new Dictionary<string, bool>
                {
                    ["SuperAdmin"] = true,
                    ["Admin"] = true,
                    ["Receptionist"] = true,
                    ["Doctor"] = true,
                    ["Nurse"] = true,
                }),

            [NotificationTypeCodes.AppointmentNoShow] = new(
                NotificationTypeCodes.AppointmentNoShow, "Appointments",
                [CureFlowPermissions.AppointmentView],
                NotificationSeverity.Warning,
                NotificationRecipientStrategy.AppointmentDoctor,
                true,
                new Dictionary<string, bool>
                {
                    ["SuperAdmin"] = true,
                    ["Admin"] = true,
                    ["Receptionist"] = true,
                    ["Doctor"] = true,
                }),

            [NotificationTypeCodes.TaskAssigned] = new(
                NotificationTypeCodes.TaskAssigned, "Tasks",
                [CureFlowPermissions.DashboardView],
                NotificationSeverity.Info,
                NotificationRecipientStrategy.Assignee,
                true,
                adminRoles),

            [NotificationTypeCodes.CampaignCompleted] = new(
                NotificationTypeCodes.CampaignCompleted, "Campaigns",
                [CureFlowPermissions.CampaignView],
                NotificationSeverity.Info,
                NotificationRecipientStrategy.PermissionHolders,
                true,
                new Dictionary<string, bool>
                {
                    ["SuperAdmin"] = true,
                    ["Admin"] = true,
                    ["Marketing"] = true,
                }),

            [NotificationTypeCodes.CampaignFailed] = new(
                NotificationTypeCodes.CampaignFailed, "Campaigns",
                [CureFlowPermissions.CampaignManage],
                NotificationSeverity.Warning,
                NotificationRecipientStrategy.PermissionHolders,
                true,
                new Dictionary<string, bool>
                {
                    ["SuperAdmin"] = true,
                    ["Admin"] = true,
                    ["Marketing"] = true,
                }),

            [NotificationTypeCodes.ClinicalLabUploaded] = new(
                NotificationTypeCodes.ClinicalLabUploaded, "Clinical",
                [CureFlowPermissions.ClinicalView],
                NotificationSeverity.Info,
                NotificationRecipientStrategy.PermissionHolders,
                true,
                new Dictionary<string, bool>
                {
                    ["SuperAdmin"] = true,
                    ["Admin"] = true,
                    ["Doctor"] = true,
                    ["Nurse"] = true,
                }),

            [NotificationTypeCodes.PatientCreated] = new(
                NotificationTypeCodes.PatientCreated, "Patients",
                [CureFlowPermissions.PatientView],
                NotificationSeverity.Info,
                NotificationRecipientStrategy.PermissionHolders,
                true,
                new Dictionary<string, bool>
                {
                    ["SuperAdmin"] = true,
                    ["Admin"] = true,
                    ["Receptionist"] = true,
                    ["Doctor"] = true,
                    ["Nurse"] = true,
                    ["Marketing"] = true,
                    ["Staff"] = true,
                    ["Viewer"] = true,
                }),
        };
    }
}

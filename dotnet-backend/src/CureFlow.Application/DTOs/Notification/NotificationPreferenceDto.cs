namespace CureFlow.Application.DTOs.Notification;

public class NotificationPreferenceDto
{
    public string NotificationType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool InAppEnabled { get; set; }
    public bool? InAppUserOverride { get; set; }
    public bool InAppRoleDefault { get; set; }
    public bool CanConfigure { get; set; }
}

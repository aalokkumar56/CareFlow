namespace CureFlow.Application.DTOs.Notification;

public class UpdateNotificationPreferencesRequest
{
    public List<NotificationPreferenceUpdateItem> Preferences { get; set; } = new();
}

public class NotificationPreferenceUpdateItem
{
    public string NotificationType { get; set; } = string.Empty;
    public bool InAppEnabled { get; set; }
}

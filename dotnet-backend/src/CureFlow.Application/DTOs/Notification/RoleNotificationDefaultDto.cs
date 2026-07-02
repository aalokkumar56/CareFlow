namespace CureFlow.Application.DTOs.Notification;

public class RoleNotificationDefaultDto
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool InAppEnabled { get; set; }
    public bool CanEnable { get; set; }
}

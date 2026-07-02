using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

public class RoleNotificationDefault : BaseEntity
{
    public Guid RoleId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public bool Enabled { get; set; } = true;
    public bool IsSystem { get; set; } = true;
}

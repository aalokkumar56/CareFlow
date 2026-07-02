using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

public class Conversation : TenantEntity
{
    public Guid? PatientId { get; set; }
    public string WaPhone { get; set; } = string.Empty;
    public string? Name { get; set; }
    public ConversationCategory Category { get; set; } = ConversationCategory.General;
    public Priority Priority { get; set; } = Priority.Medium;
    public Guid? AssignedStaffId { get; set; }
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public string? LastMessagePreview { get; set; }
    public int UnreadCount { get; set; } = 0;
    public List<string> Tags { get; set; } = new();
    public string? AiSummary { get; set; }
    public DateTime? AwaitingReplySince { get; set; }
    public bool Escalated { get; set; } = false;
}





using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

public class EmailMessage : TenantEntity
{
    public Guid? PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public MessageDirection Direction { get; set; } = MessageDirection.Outbound;
    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
}

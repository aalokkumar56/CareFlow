using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

public class CampaignRecipient : TenantEntity
{
    public Guid CampaignId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public RecipientStatus Status { get; set; } = RecipientStatus.Queued;
    public string? WaMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? RepliedAt { get; set; }
}

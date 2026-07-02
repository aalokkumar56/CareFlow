using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

public class Campaign : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MessageBody { get; set; } = string.Empty;
    /// <summary>JSON-serialized AudienceFilter.</summary>
    public string AudienceJson { get; set; } = "{}";
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int RepliedCount { get; set; }
    public int FailedCount { get; set; }
}

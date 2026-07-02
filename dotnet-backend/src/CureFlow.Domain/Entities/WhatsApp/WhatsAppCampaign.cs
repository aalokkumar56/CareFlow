using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class WhatsAppCampaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // bot, api, regular
    public string Status { get; set; } = string.Empty;
    public int ExternalId { get; set; }
    public DateTime? ExternalDeletedAt { get; set; }
    public DateTime ExternalCreatedAt { get; set; }
    public DateTime ExternalUpdatedAt { get; set; }
    public DateTime CachedAt { get; set; }
}

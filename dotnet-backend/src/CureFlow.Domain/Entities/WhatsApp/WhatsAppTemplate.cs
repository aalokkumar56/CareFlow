using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class WhatsAppTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Components { get; set; } = string.Empty; // JSON serialized
    public int ExternalId { get; set; }
    public DateTime ExternalCreatedAt { get; set; }
    public DateTime ExternalUpdatedAt { get; set; }
    public DateTime CachedAt { get; set; }
}

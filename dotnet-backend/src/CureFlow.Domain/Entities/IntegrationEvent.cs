using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class IntegrationEvent : TenantEntity
{
    public string EventType { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public bool Processed { get; set; } = false;
}

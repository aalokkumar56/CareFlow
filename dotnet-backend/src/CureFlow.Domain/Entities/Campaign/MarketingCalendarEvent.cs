using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

/// <summary>Tenant marketing calendar entry (holidays, health days) for suggested campaign drafts.</summary>
public class MarketingCalendarEvent : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Category { get; set; } = "holiday";
    public string Region { get; set; } = "IN";
    public string Source { get; set; } = "seed";
    public string? SuggestedMessage { get; set; }
}

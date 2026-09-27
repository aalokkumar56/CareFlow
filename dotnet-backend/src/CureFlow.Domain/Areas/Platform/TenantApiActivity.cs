using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Saas;

/// <summary>Operational request metadata; never contains request/response bodies or patient identifiers.</summary>
public sealed class TenantApiActivity : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid? ActorId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorSummary { get; set; }
}

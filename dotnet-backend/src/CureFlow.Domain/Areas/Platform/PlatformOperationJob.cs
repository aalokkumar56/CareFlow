using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Saas;

public class PlatformOperationJob : BaseEntity
{
    public string JobType { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string Status { get; set; } = "Queued";
    public string? PayloadJson { get; set; }
    public string? Error { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Saas;

public class PlatformAuditLog : BaseEntity
{
    public Guid? PlatformUserId { get; set; }
    public Guid? TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
}

using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Saas;

/// <summary>Platform decision granting a module to a customer tenant.</summary>
public class TenantEntitlement : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid PlatformModuleId { get; set; }
    public int Version { get; set; }
    public bool IsEnabled { get; set; }
    public int? Limit { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

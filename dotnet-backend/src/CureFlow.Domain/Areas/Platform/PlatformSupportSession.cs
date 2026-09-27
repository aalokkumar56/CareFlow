using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Saas;

public class PlatformSupportSession : BaseEntity
{
    public Guid PlatformUserId { get; set; }
    public Guid TenantId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Scope { get; set; } = "ReadOnly";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

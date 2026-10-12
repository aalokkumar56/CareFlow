using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities.Saas;

public sealed class PlatformSession : BaseEntity
{
    public Guid PlatformUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int SecurityRevision { get; set; } = 1;
}

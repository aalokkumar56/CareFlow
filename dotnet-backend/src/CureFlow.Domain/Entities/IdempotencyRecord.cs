using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;

public sealed class IdempotencyRecord : TenantEntity
{
    public Guid ActorId { get; set; }
    public string CommandNamespace { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string State { get; set; } = "processing";
    public string ResponseJson { get; set; } = string.Empty;
    public int ResponseStatus { get; set; }
    public DateTime ExpiresAt { get; set; }
}

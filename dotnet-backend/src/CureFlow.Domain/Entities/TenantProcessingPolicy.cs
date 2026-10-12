using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;

public sealed class TenantProcessingPolicy : TenantEntity
{
    public string Provider { get; set; } = "";
    public string Purpose { get; set; } = "";
    public int Revision { get; set; }
    public bool Allowed { get; set; }
    public string ActorKind { get; set; } = "person";
    public string ApprovalReference { get; set; } = "";
    public string BasisReference { get; set; } = "";
    public int BasisRevision { get; set; }
    public Guid ApprovedBy { get; set; }
    public DateTime ApprovedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

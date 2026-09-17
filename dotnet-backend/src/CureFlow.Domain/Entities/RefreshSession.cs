namespace CureFlow.Domain.Entities;

public class RefreshSession
{
    public string TokenHash { get; set; } = "";
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Consumed { get; set; }
    public bool Revoked { get; set; }
}

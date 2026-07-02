namespace CureFlow.Domain.Common;

/// <summary>Base entity for all domain models. Tracks creation, updates, and soft-delete.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
}

/// <summary>Entities that belong to a specific tenant (hospital).</summary>
public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}

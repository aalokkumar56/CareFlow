using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Saas;

/// <summary>A physical location belonging to a customer tenant.</summary>
public class Facility : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; } = "IN";
    public bool IsPrimary { get; set; }
}

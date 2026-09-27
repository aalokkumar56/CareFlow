using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Saas;

/// <summary>A product capability owned by the CureFlow platform.</summary>
public class PlatformModule : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCore { get; set; }
}

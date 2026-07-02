using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

/// <summary>
/// Defines a merge field available in quick-reply and campaign message templates.
/// System rows are seeded; tenants may add custom placeholders with optional static values.
/// </summary>
public class TemplatePlaceholder : TenantEntity
{
    /// <summary>Merge key used in templates, e.g. "name" for {name}.</summary>
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Example { get; set; }
    public bool IsSystem { get; set; }
    /// <summary>When set, this value is used for custom tenant placeholders at send time.</summary>
    public string? StaticValue { get; set; }
}

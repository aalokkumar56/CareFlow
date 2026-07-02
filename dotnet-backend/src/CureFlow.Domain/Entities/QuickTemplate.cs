using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class QuickTemplate : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
}

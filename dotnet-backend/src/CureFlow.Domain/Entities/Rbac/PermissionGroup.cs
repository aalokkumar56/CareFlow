using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Rbac;

public class PermissionGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}

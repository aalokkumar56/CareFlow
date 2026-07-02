using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Rbac;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? PermissionGroupId { get; set; }
    public PermissionGroup? Group { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

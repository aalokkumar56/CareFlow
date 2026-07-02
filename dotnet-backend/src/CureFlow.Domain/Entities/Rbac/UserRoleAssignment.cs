using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Rbac;

public class UserRoleAssignment : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

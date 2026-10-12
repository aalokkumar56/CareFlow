using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Application.Interfaces;

namespace CureFlow.Infrastructure.Identity;

public interface IUserPermissionService
{
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(User user, CancellationToken ct = default, ICureFlowDbSession? authority = null);
    Task AssignLegacyRoleAsync(User user, CancellationToken ct = default, ICureFlowDbSession? authority = null);
}

public class UserPermissionService(ICureFlowDbSession db) : IUserPermissionService
{
    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(User user, CancellationToken ct = default, ICureFlowDbSession? authority = null)
    {
        var store = authority ?? db;
        var roleIds = (await store.QueryAsync<Guid>(
            """
            SELECT "RoleId" FROM "UserRoleAssignments"
            WHERE "UserId" = @UserId AND "TenantId" = @TenantId AND "IsDeleted" = false AND "IsActive" = true
            """,
            new { UserId = user.Id, TenantId = user.TenantId },
            ignoreTenant: true,
            ct)).ToList();

        if (roleIds.Count == 0)
            return Array.Empty<string>();

        return (await store.QueryAsync<string>(
            """
            SELECT DISTINCT p."Code"
            FROM "RolePermissions" rp
            INNER JOIN "Permissions" p ON p."Id" = rp."PermissionId"
            INNER JOIN "Roles" r ON r."Id" = rp."RoleId"
            WHERE rp."RoleId" = ANY(@RoleIds)
              AND rp."IsDeleted" = false AND rp."IsActive" = true
              AND p."IsDeleted" = false AND p."IsActive" = true AND r."IsDeleted" = false AND r."IsActive" = true
            ORDER BY p."Code"
            """,
            new { RoleIds = roleIds.ToArray() },
            ignoreTenant: true,
            ct)).ToList();
    }

    public async Task AssignLegacyRoleAsync(User user, CancellationToken ct = default, ICureFlowDbSession? authority = null)
    {
        var store = authority ?? db;
        var hasAssignment = await store.QueryFirstOrDefaultAsync<int?>(
            """
            SELECT 1 FROM "UserRoleAssignments"
            WHERE "UserId" = @UserId AND "TenantId" = @TenantId
            LIMIT 1
            """,
            new { UserId = user.Id, TenantId = user.TenantId },
            ignoreTenant: true,
            ct);
        if (hasAssignment != null) return;

        var roleName = CureFlowPermissions.MapLegacyRole(user.Role);
        var role = await store.QueryFirstOrDefaultAsync<Role>(
            """
            SELECT * FROM "Roles"
            WHERE "Name" = @Name AND "IsDeleted" = false
            LIMIT 1
            """,
            new { Name = roleName },
            ignoreTenant: true,
            ct);
        if (role == null) return;

        await store.InsertAsync(new UserRoleAssignment
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            RoleId = role.Id,
        }, ignoreTenant: true, ct);
    }
}



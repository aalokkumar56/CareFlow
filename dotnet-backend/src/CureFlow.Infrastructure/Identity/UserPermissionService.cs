using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Application.Interfaces;

namespace CureFlow.Infrastructure.Identity;

public interface IUserPermissionService
{
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(User user, CancellationToken ct = default);
    Task AssignLegacyRoleAsync(User user, CancellationToken ct = default);
}

public class UserPermissionService(ICureFlowDbSession db) : IUserPermissionService
{
    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(User user, CancellationToken ct = default)
    {
        var roleIds = (await db.QueryAsync<Guid>(
            """
            SELECT "RoleId" FROM "UserRoleAssignments"
            WHERE "UserId" = @UserId AND "TenantId" = @TenantId AND "IsDeleted" = false
            """,
            new { UserId = user.Id, TenantId = user.TenantId },
            ignoreTenant: true,
            ct)).ToList();

        if (roleIds.Count == 0)
        {
            var roleName = CureFlowPermissions.MapLegacyRole(user.Role);
            var fallbackRoleId = await db.QueryFirstOrDefaultAsync<Guid>(
                """
                SELECT "Id" FROM "Roles"
                WHERE "Name" = @Name AND "IsDeleted" = false
                LIMIT 1
                """,
                new { Name = roleName },
                ignoreTenant: true,
                ct);
            if (fallbackRoleId != Guid.Empty)
                roleIds.Add(fallbackRoleId);
        }

        if (roleIds.Count == 0)
            return Array.Empty<string>();

        return (await db.QueryAsync<string>(
            """
            SELECT DISTINCT p."Code"
            FROM "RolePermissions" rp
            INNER JOIN "Permissions" p ON p."Id" = rp."PermissionId"
            WHERE rp."RoleId" = ANY(@RoleIds)
              AND rp."IsDeleted" = false
              AND p."IsDeleted" = false
            ORDER BY p."Code"
            """,
            new { RoleIds = roleIds.ToArray() },
            ignoreTenant: true,
            ct)).ToList();
    }

    public async Task AssignLegacyRoleAsync(User user, CancellationToken ct = default)
    {
        var hasAssignment = await db.QueryFirstOrDefaultAsync<int?>(
            """
            SELECT 1 FROM "UserRoleAssignments"
            WHERE "UserId" = @UserId AND "TenantId" = @TenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { UserId = user.Id, TenantId = user.TenantId },
            ignoreTenant: true,
            ct);
        if (hasAssignment != null) return;

        var roleName = CureFlowPermissions.MapLegacyRole(user.Role);
        var role = await db.QueryFirstOrDefaultAsync<Role>(
            """
            SELECT * FROM "Roles"
            WHERE "Name" = @Name AND "IsDeleted" = false
            LIMIT 1
            """,
            new { Name = roleName },
            ignoreTenant: true,
            ct);
        if (role == null) return;

        await db.InsertAsync(new UserRoleAssignment
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            RoleId = role.Id,
        }, ignoreTenant: true, ct);
    }
}

using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services;

public class UserManagementService : IUserManagementService
{
    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditService _audit;
    private readonly IUserPermissionService _permissions;

    public UserManagementService(
        ICureFlowDbSession db,
        ITenantContext tenant,
        IPasswordHasher hasher,
        IAuditService audit,
        IUserPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _hasher = hasher;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<UserListItemDto>> ListAsync(
        string? q, UserRole? role, bool? isActive, int limit = 100, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var sql = new System.Text.StringBuilder(
            """
            SELECT * FROM "Users"
            WHERE "IsDeleted" = false AND "TenantId" = @TenantId
            """);
        var param = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim().ToLower()}%";
            sql.Append("""
                 AND (LOWER("Name") LIKE @term OR LOWER("Email") LIKE @term
                      OR ("Phone" IS NOT NULL AND "Phone" LIKE @term))
                """);
            param["term"] = term;
        }

        if (role.HasValue)
        {
            sql.Append(" AND \"Role\" = @role");
            param["role"] = (int)role.Value;
        }

        if (isActive.HasValue)
        {
            sql.Append(" AND \"IsActive\" = @isActive");
            param["isActive"] = isActive.Value;
        }

        sql.Append(" ORDER BY \"Name\" LIMIT @limit");
        param["limit"] = safeLimit;

        var rows = await _db.QueryAsync<User>(sql.ToString(), param, ct: ct);
        return rows.Select(ToListItem).ToList();
    }

    public async Task<UserListItemDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindUserAsync(id, ct);
        return ToListItem(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        if (await EmailExistsAsync(req.Email, null, ct))
            throw new ValidationException("Email already exists in this hospital");

        if (!EnumParseHelper.TryParseSnakeCase<UserRole>(req.Role, out var role))
            throw new ValidationException("Invalid role");

        var user = new User
        {
            Name = req.Name.Trim(),
            Email = req.Email.ToLower().Trim(),
            PasswordHash = _hasher.Hash(req.Password),
            Role = role,
            Specialty = req.Specialty,
            Phone = req.Phone,
            Qualifications = req.Qualifications,
        };
        await _db.InsertAsync(user, ct: ct);
        await _permissions.AssignLegacyRoleAsync(user, ct);
        await _audit.LogAsync("user.create", "user", user.Id.ToString(), new { req.Email, req.Role }, ct);
        var permissionCodes = await _permissions.GetPermissionCodesAsync(user, ct);
        return new UserDto(user.Id, user.Name, user.Email, user.Role, user.IsActive, user.Specialty, user.Phone, permissionCodes);
    }

    public async Task<UserListItemDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default)
    {
        var user = await FindUserAsync(id, ct);
        GuardTarget(user);

        if (!string.IsNullOrWhiteSpace(req.Name))
            user.Name = req.Name.Trim();

        if (!string.IsNullOrWhiteSpace(req.Email) && !req.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await EmailExistsAsync(req.Email, id, ct))
                throw new ValidationException("Email already exists in this hospital");
            user.Email = req.Email.ToLower().Trim();
        }

        if (!string.IsNullOrWhiteSpace(req.Role))
        {
            if (!EnumParseHelper.TryParseSnakeCase<UserRole>(req.Role, out var newRole))
                throw new ValidationException("Invalid role");
            GuardRoleChange(user, newRole);
            user.Role = newRole;
        }

        if (req.IsActive.HasValue)
        {
            if (!req.IsActive.Value) GuardDisable(user);
            user.IsActive = req.IsActive.Value;
        }

        if (req.Specialty != null) user.Specialty = req.Specialty;
        if (req.Phone != null) user.Phone = req.Phone;
        if (req.Qualifications != null) user.Qualifications = req.Qualifications;
        if (!string.IsNullOrWhiteSpace(req.Password))
            user.PasswordHash = _hasher.Hash(req.Password);

        await _db.UpdateAsync(user, ct: ct);

        if (!string.IsNullOrWhiteSpace(req.Role))
            await SyncRbacRoleAsync(user, ct);

        await _audit.LogAsync("user.update", "user", user.Id.ToString(), req, ct);
        return ToListItem(user);
    }

    public async Task<UserListItemDto> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var user = await FindUserAsync(id, ct);
        GuardTarget(user);
        if (!isActive) GuardDisable(user);
        user.IsActive = isActive;
        await _db.UpdateAsync(user, ct: ct);
        await _audit.LogAsync(isActive ? "user.enable" : "user.disable", "user", user.Id.ToString(), null, ct);
        return ToListItem(user);
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(Guid id, ResetPasswordRequest req, CancellationToken ct = default)
    {
        var user = await FindUserAsync(id, ct);
        GuardTarget(user);

        string tempPassword;
        var generated = req.GenerateTemporary || string.IsNullOrWhiteSpace(req.NewPassword);
        if (generated)
        {
            tempPassword = GenerateTemporaryPassword();
            user.PasswordHash = _hasher.Hash(tempPassword);
        }
        else
        {
            tempPassword = req.NewPassword!;
            user.PasswordHash = _hasher.Hash(tempPassword);
        }

        await _db.UpdateAsync(user, ct: ct);
        await _audit.LogAsync("user.reset_password", "user", user.Id.ToString(), new { generated }, ct);
        return new ResetPasswordResult(tempPassword, generated);
    }

    public async Task<UserListItemDto> AssignRoleAsync(Guid id, AssignRolesRequest req, CancellationToken ct = default)
    {
        var user = await FindUserAsync(id, ct);
        GuardTarget(user);
        GuardRoleChange(user, req.Role);
        user.Role = req.Role;
        await _db.UpdateAsync(user, ct: ct);
        await SyncRbacRoleAsync(user, ct);
        await _audit.LogAsync("user.assign_role", "user", user.Id.ToString(), new { role = req.Role }, ct);
        return ToListItem(user);
    }

    public async Task AssignPermissionsAsync(Guid id, AssignPermissionsRequest req, CancellationToken ct = default)
    {
        var user = await FindUserAsync(id, ct);
        GuardTarget(user);

        var validCodes = (await _db.QueryAsync<string>(
            """
            SELECT "Code" FROM "Permissions"
            WHERE "Code" = ANY(@codes) AND "IsDeleted" = false
            """,
            new { codes = req.Permissions.ToArray() },
            ignoreTenant: true,
            ct: ct)).ToList();

        if (validCodes.Count == 0)
            throw new ValidationException("No valid permission codes supplied");

        var roleName = $"Custom-{user.Id:N}"[..Math.Min(32, $"Custom-{user.Id:N}".Length)];
        var existingRole = await _db.QueryFirstOrDefaultAsync<Role>(
            """
            SELECT * FROM "Roles"
            WHERE "Name" = @roleName AND "IsDeleted" = false
            """,
            new { roleName },
            ignoreTenant: true,
            ct: ct);

        if (existingRole == null)
        {
            existingRole = new Role { Name = roleName, Description = $"Custom permissions for {user.Email}", IsSystem = false };
            await _db.InsertAsync(existingRole, ignoreTenant: true, ct: ct);
        }

        await _db.ExecuteAsync(
            """DELETE FROM "RolePermissions" WHERE "RoleId" = @roleId""",
            new { roleId = existingRole.Id },
            ignoreTenant: true,
            ct: ct);

        var permissionIds = await _db.QueryAsync<Guid>(
            """
            SELECT "Id" FROM "Permissions"
            WHERE "Code" = ANY(@codes) AND "IsDeleted" = false
            """,
            new { codes = validCodes.ToArray() },
            ignoreTenant: true,
            ct: ct);

        foreach (var permissionId in permissionIds)
        {
            await _db.InsertAsync(new RolePermission { RoleId = existingRole.Id, PermissionId = permissionId },
                ignoreTenant: true, ct: ct);
        }

        await _db.ExecuteAsync(
            """DELETE FROM "UserRoleAssignments" WHERE "UserId" = @userId AND "TenantId" = @TenantId""",
            new { userId = user.Id },
            ct: ct);
        await _db.InsertAsync(new UserRoleAssignment { UserId = user.Id, RoleId = existingRole.Id }, ct: ct);

        await _audit.LogAsync("user.assign_permissions", "user", user.Id.ToString(), new { permissions = validCodes }, ct);
    }

    public async Task<IReadOnlyList<RoleDefinitionDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var tenantCustomRoleIds = (await _db.QueryAsync<Guid>(
            """
            SELECT DISTINCT "RoleId" FROM "UserRoleAssignments"
            WHERE "IsDeleted" = false AND "TenantId" = @TenantId
            """,
            ct: ct)).ToHashSet();

        var roles = await _db.QueryAsync<Role>(
            """
            SELECT * FROM "Roles"
            WHERE "IsDeleted" = false AND ("IsSystem" = true OR "Id" = ANY(@customIds))
            ORDER BY "Name"
            """,
            new { customIds = tenantCustomRoleIds.ToArray() },
            ignoreTenant: true,
            ct: ct);

        var result = new List<RoleDefinitionDto>();
        foreach (var role in roles)
        {
            var codes = await _db.QueryAsync<string>(
                """
                SELECT DISTINCT p."Code"
                FROM "RolePermissions" rp
                INNER JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                WHERE rp."RoleId" = @roleId AND rp."IsDeleted" = false AND p."IsDeleted" = false
                ORDER BY p."Code"
                """,
                new { roleId = role.Id },
                ignoreTenant: true,
                ct: ct);

            result.Add(new RoleDefinitionDto(
                role.Name,
                MapRoleNameToLegacy(role.Name),
                role.Name,
                role.Description ?? string.Empty,
                codes.ToList(),
                role.IsSystem));
        }

        return result;
    }

    public async Task<IReadOnlyList<PermissionDefinitionDto>> ListPermissionsAsync(CancellationToken ct = default)
    {
        var permissions = await _db.QueryAsync<Permission>(
            """
            SELECT * FROM "Permissions"
            WHERE "IsDeleted" = false
            ORDER BY "Code"
            """,
            ignoreTenant: true,
            ct: ct);

        var groupIds = permissions.Where(p => p.PermissionGroupId.HasValue)
            .Select(p => p.PermissionGroupId!.Value).Distinct().ToArray();
        var groups = groupIds.Length == 0
            ? new Dictionary<Guid, PermissionGroup>()
            : (await _db.QueryAsync<PermissionGroup>(
                """
                SELECT * FROM "PermissionGroups"
                WHERE "Id" = ANY(@groupIds) AND "IsDeleted" = false
                """,
                new { groupIds },
                ignoreTenant: true,
                ct: ct)).ToDictionary(g => g.Id);

        return permissions.Select(p => new PermissionDefinitionDto(
            p.Code,
            p.Name,
            p.PermissionGroupId.HasValue && groups.TryGetValue(p.PermissionGroupId.Value, out var g) ? g.Name : null))
            .ToList();
    }

    public async Task<RoleDefinitionDto> CreateRoleAsync(CreateRoleRequest req, CancellationToken ct = default)
    {
        var name = RoleNameRules.Normalize(req.Name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Role name is required");

        if (await RoleNameExistsAsync(name, ct))
            throw new ValidationException("A role with this name already exists");

        var role = new Role
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            IsSystem = false,
        };
        await _db.InsertAsync(role, ignoreTenant: true, ct: ct);

        await ReplaceRolePermissionsAsync(role, req.Permissions ?? Array.Empty<string>(), ct);
        await _audit.LogAsync("role.create", "role", role.Id.ToString(), new { role.Name }, ct);
        return await BuildRoleDefinitionAsync(role, ct);
    }

    public async Task<RoleDefinitionDto> UpdateRoleAsync(string name, UpdateRoleRequest req, CancellationToken ct = default)
    {
        var role = await FindRoleByNameAsync(name, ct);
        GuardRoleMutation(role);

        if (!role.IsSystem && req.Description != null)
            role.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();

        await ReplaceRolePermissionsAsync(role, req.Permissions ?? Array.Empty<string>(), ct);
        await _audit.LogAsync("role.update", "role", role.Id.ToString(), new { role.Name }, ct);
        return await BuildRoleDefinitionAsync(role, ct);
    }

    public async Task DeleteRoleAsync(string name, CancellationToken ct = default)
    {
        var role = await FindRoleByNameAsync(name, ct);
        if (role.IsSystem)
            throw new ValidationException("Built-in roles cannot be deleted");

        var inUse = await _db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "UserRoleAssignments"
            WHERE "RoleId" = @roleId AND "IsDeleted" = false AND "TenantId" = @TenantId
            LIMIT 1
            """,
            new { roleId = role.Id },
            ct: ct) == 1;
        if (inUse)
            throw new ValidationException("Cannot delete a role that is assigned to users");

        await _db.ExecuteAsync(
            """DELETE FROM "RolePermissions" WHERE "RoleId" = @roleId""",
            new { roleId = role.Id },
            ignoreTenant: true,
            ct: ct);
        role.IsDeleted = true;
        await _db.UpdateAsync(role, ignoreTenant: true, ct: ct);
        await _audit.LogAsync("role.delete", "role", role.Id.ToString(), new { role.Name }, ct);
    }

    private async Task ReplaceRolePermissionsAsync(Role role, IReadOnlyList<string> permissionCodes, CancellationToken ct)
    {
        var codes = permissionCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissionIds = await _db.QueryAsync<Guid>(
            """
            SELECT "Id" FROM "Permissions"
            WHERE "Code" = ANY(@codes) AND "IsDeleted" = false
            """,
            new { codes = codes.ToArray() },
            ignoreTenant: true,
            ct: ct);

        if (codes.Count > 0 && !permissionIds.Any())
            throw new ValidationException("No valid permission codes supplied");

        await _db.ExecuteAsync(
            """DELETE FROM "RolePermissions" WHERE "RoleId" = @roleId""",
            new { roleId = role.Id },
            ignoreTenant: true,
            ct: ct);

        foreach (var permissionId in permissionIds)
            await _db.InsertAsync(new RolePermission { RoleId = role.Id, PermissionId = permissionId }, ignoreTenant: true, ct: ct);
    }

    private async Task<RoleDefinitionDto> BuildRoleDefinitionAsync(Role role, CancellationToken ct)
    {
        var codes = await _db.QueryAsync<string>(
            """
            SELECT DISTINCT p."Code"
            FROM "RolePermissions" rp
            INNER JOIN "Permissions" p ON p."Id" = rp."PermissionId"
            WHERE rp."RoleId" = @roleId AND rp."IsDeleted" = false AND p."IsDeleted" = false
            ORDER BY p."Code"
            """,
            new { roleId = role.Id },
            ignoreTenant: true,
            ct: ct);

        return new RoleDefinitionDto(
            role.Name,
            MapRoleNameToLegacy(role.Name),
            role.Name,
            role.Description ?? string.Empty,
            codes.ToList(),
            role.IsSystem);
    }

    private async Task<Role> FindRoleByNameAsync(string name, CancellationToken ct)
    {
        var normalized = RoleNameRules.Normalize(name);
        return await _db.QueryFirstOrDefaultAsync<Role>(
            """
            SELECT * FROM "Roles"
            WHERE "Name" = @normalized AND "IsDeleted" = false
            """,
            new { normalized },
            ignoreTenant: true,
            ct: ct) ?? throw new NotFoundException("Role");
    }

    private static void GuardRoleMutation(Role role)
    {
        if (RoleNameRules.IsProtected(role.Name))
            throw new ValidationException("SuperAdmin role permissions cannot be modified");
    }

    private async Task SyncRbacRoleAsync(User user, CancellationToken ct)
    {
        await _db.ExecuteAsync(
            """DELETE FROM "UserRoleAssignments" WHERE "UserId" = @userId AND "TenantId" = @TenantId""",
            new { userId = user.Id },
            ct: ct);
        await _permissions.AssignLegacyRoleAsync(user, ct);
    }

    private async Task<User> FindUserAsync(Guid id, CancellationToken ct) =>
        await _db.GetByIdAsync<User>(id, ct: ct) ?? throw new NotFoundException("User");

    private async Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken ct)
    {
        if (excludeId.HasValue)
        {
            return await _db.QueryFirstOrDefaultAsync<int>(
                """
                SELECT 1 FROM "Users"
                WHERE LOWER("Email") = @email AND "Id" <> @excludeId
                  AND "IsDeleted" = false AND "TenantId" = @TenantId
                LIMIT 1
                """,
                new { email = email.ToLower(), excludeId = excludeId.Value },
                ct: ct) == 1;
        }

        return await _db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "Users"
            WHERE LOWER("Email") = @email AND "IsDeleted" = false AND "TenantId" = @TenantId
            LIMIT 1
            """,
            new { email = email.ToLower() },
            ct: ct) == 1;
    }

    private async Task<bool> RoleNameExistsAsync(string name, CancellationToken ct) =>
        await _db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "Roles"
            WHERE "Name" = @name AND "IsDeleted" = false
            LIMIT 1
            """,
            new { name },
            ignoreTenant: true,
            ct: ct) == 1;

    private void GuardTarget(User user)
    {
        if (user.Id == _tenant.UserId)
            throw new ValidationException("You cannot modify your own account through admin actions");
    }

    private void GuardDisable(User user)
    {
        if (user.Role == UserRole.TenantOwner)
            throw new ValidationException("Cannot disable the tenant owner");
    }

    private void GuardRoleChange(User user, UserRole newRole)
    {
        if (user.Role == UserRole.TenantOwner && newRole != UserRole.TenantOwner)
            throw new ValidationException("Cannot change tenant owner role");
        if (!Enum.TryParse<UserRole>(_tenant.UserRole, true, out var actorRole))
            throw new ForbiddenException();
        if (actorRole != UserRole.TenantOwner && newRole == UserRole.TenantOwner)
            throw new ValidationException("Only tenant owner can assign tenant owner role");
    }

    private static UserListItemDto ToListItem(User u) =>
        new(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.Specialty, u.Phone, u.LastLoginAt, u.CreatedAt);

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
        var bytes = new byte[12];
        Random.Shared.NextBytes(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static string? MapRoleNameToLegacy(string roleName) => roleName switch
    {
        RoleNames.SuperAdmin => "tenant_owner",
        RoleNames.Admin => "admin",
        RoleNames.Doctor => "doctor",
        RoleNames.Receptionist => "reception",
        RoleNames.Marketing => "marketing",
        RoleNames.Staff => "staff",
        _ => null,
    };
}

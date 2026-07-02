using CureFlow.Application.DTOs;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserListItemDto>> ListAsync(
        string? q, UserRole? role, bool? isActive, int limit = 100, CancellationToken ct = default);

    Task<UserListItemDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct = default);
    Task<UserListItemDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default);
    Task<UserListItemDto> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<ResetPasswordResult> ResetPasswordAsync(Guid id, ResetPasswordRequest req, CancellationToken ct = default);
    Task<UserListItemDto> AssignRoleAsync(Guid id, AssignRolesRequest req, CancellationToken ct = default);
    Task AssignPermissionsAsync(Guid id, AssignPermissionsRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<RoleDefinitionDto>> ListRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PermissionDefinitionDto>> ListPermissionsAsync(CancellationToken ct = default);
    Task<RoleDefinitionDto> CreateRoleAsync(CreateRoleRequest req, CancellationToken ct = default);
    Task<RoleDefinitionDto> UpdateRoleAsync(string name, UpdateRoleRequest req, CancellationToken ct = default);
    Task DeleteRoleAsync(string name, CancellationToken ct = default);
}

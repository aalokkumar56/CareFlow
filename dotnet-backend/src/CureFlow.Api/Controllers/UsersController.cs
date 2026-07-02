using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _users;

    public UsersController(IUserManagementService users) => _users = users;

    [HttpGet]
    [Authorize(Policy = "Permission:User.View")]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] string? role,
        [FromQuery] bool? is_active,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        UserRole? parsedRole = null;
        if (!string.IsNullOrWhiteSpace(role) && EnumParseHelper.TryParseSnakeCase(role, out UserRole r))
            parsedRole = r;
        return Ok(await _users.ListAsync(q, parsedRole, is_active, limit, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:User.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _users.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "Permission:User.Create")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct) =>
        Ok(await _users.CreateAsync(req, ct));

    [HttpPatch("{id:guid}")]
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:User.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct) =>
        Ok(await _users.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/disable")]
    [Authorize(Policy = "Permission:User.Edit")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) =>
        Ok(await _users.SetActiveAsync(id, false, ct));

    [HttpPost("{id:guid}/enable")]
    [Authorize(Policy = "Permission:User.Edit")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct) =>
        Ok(await _users.SetActiveAsync(id, true, ct));

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "Permission:User.Edit")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest req, CancellationToken ct) =>
        Ok(await _users.ResetPasswordAsync(id, req, ct));

    [HttpPost("{id:guid}/assign-roles")]
    [Authorize(Policy = "Permission:User.Edit")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest req, CancellationToken ct) =>
        Ok(await _users.AssignRoleAsync(id, req, ct));

    [HttpPost("{id:guid}/assign-permissions")]
    [Authorize(Policy = "Permission:User.Edit")]
    public async Task<IActionResult> AssignPermissions(Guid id, [FromBody] AssignPermissionsRequest req, CancellationToken ct)
    {
        await _users.AssignPermissionsAsync(id, req, ct);
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:User.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _users.SetActiveAsync(id, false, ct);
        return NoContent();
    }
}

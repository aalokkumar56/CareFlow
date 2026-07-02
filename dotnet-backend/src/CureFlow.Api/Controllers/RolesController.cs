using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/admin/roles")]
public class RolesController : ControllerBase
{
    private readonly IUserManagementService _users;
    public RolesController(IUserManagementService users) => _users = users;

    [HttpGet]
    [Authorize(Policy = "Permission:User.View")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _users.ListRolesAsync(ct));

    [HttpPost]
    [Authorize(Policy = "Permission:User.Edit")]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest req, CancellationToken ct) =>
        Ok(await _users.CreateRoleAsync(req, ct));

    [HttpPut("{name}")]
    [Authorize(Policy = "Permission:User.Edit")]
    public async Task<IActionResult> Update(string name, [FromBody] UpdateRoleRequest req, CancellationToken ct) =>
        Ok(await _users.UpdateRoleAsync(name, req, ct));

    [HttpDelete("{name}")]
    [Authorize(Policy = "Permission:User.Edit")]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        await _users.DeleteRoleAsync(name, ct);
        return NoContent();
    }
}

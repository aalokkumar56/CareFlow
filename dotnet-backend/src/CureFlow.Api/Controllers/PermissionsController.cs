using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/admin/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly IUserManagementService _users;
    public PermissionsController(IUserManagementService users) => _users = users;

    [HttpGet]
    [Authorize(Policy = "Permission:User.View")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _users.ListPermissionsAsync(ct));
}

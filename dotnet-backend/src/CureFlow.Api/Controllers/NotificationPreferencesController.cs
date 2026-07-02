using CureFlow.Application.Common;
using CureFlow.Application.DTOs.Notification;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notification-preferences")]
public class NotificationPreferencesController(INotificationPreferenceService preferences) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct = default) =>
        Ok(await preferences.GetEffectivePreferencesAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateNotificationPreferencesRequest request, CancellationToken ct = default)
    {
        await preferences.UpdateUserPreferencesAsync(request, ct);
        return NoContent();
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct = default)
    {
        await preferences.ResetToRoleDefaultsAsync(ct);
        return NoContent();
    }

    [HttpGet("role-defaults")]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> GetRoleDefaults(CancellationToken ct = default) =>
        Ok(await preferences.GetRoleDefaultsAsync(ct));

    [HttpPut("role-defaults")]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> UpdateRoleDefaults(
        [FromBody] List<RoleNotificationDefaultDto> updates,
        CancellationToken ct = default)
    {
        await preferences.UpdateRoleDefaultsAsync(updates, ct);
        return NoContent();
    }
}

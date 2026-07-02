using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
[Authorize]
public class WhatsappDataController : ControllerBase
{
    private readonly IWhatsappApiService _waApi;
    private readonly ILogger<WhatsappDataController> _logger;

    public WhatsappDataController(IWhatsappApiService waApi, ILogger<WhatsappDataController> logger)
    {
        _waApi = waApi;
        _logger = logger;
    }

    [HttpGet("templates")]
    [Authorize(Policy = "Permission:WhatsApp.View")]
    public async Task<IActionResult> GetTemplates(
        [FromQuery(Name = "refresh")] bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var result = await _waApi.GetTemplatesAsync(forceRefresh, ct);
        return Ok(result);
    }

    [HttpGet("groups")]
    [Authorize(Policy = "Permission:WhatsApp.View")]
    public async Task<IActionResult> GetGroups(
        [FromQuery(Name = "refresh")] bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var result = await _waApi.GetGroupsAsync(forceRefresh, ct);
        return Ok(result);
    }

    [HttpGet("campaigns")]
    [Authorize(Policy = "Permission:WhatsApp.View")]
    public async Task<IActionResult> GetCampaigns(
        [FromQuery(Name = "type")] string type = "api",
        [FromQuery(Name = "refresh")] bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var result = await _waApi.GetCampaignsAsync(type, forceRefresh, ct);
        return Ok(result);
    }

    [HttpGet("contacts")]
    [Authorize(Policy = "Permission:WhatsApp.View")]
    public async Task<IActionResult> GetContacts(
        [FromQuery(Name = "refresh")] bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var result = await _waApi.GetContactsAsync(forceRefresh, ct);
        return Ok(result);
    }

    [HttpGet("health")]
    [Authorize(Policy = "Permission:WhatsApp.View")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var configured = await _waApi.GetTemplatesAsync(false, ct);
        var isConfigured = configured.Status is not ("error" or "not_configured" or "demo");
        return Ok(new { status = "ok", configured = isConfigured, provider_status = configured.Status });
    }
}

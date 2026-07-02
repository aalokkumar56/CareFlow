using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/settings/sms")]
public class SmsSettingsController : ControllerBase
{
    private readonly ICureFlowDbSession _db;
    private readonly ISmsService _sms;

    public SmsSettingsController(ICureFlowDbSession db, ISmsService sms)
    {
        _db = db;
        _sms = sms;
    }

    public record SmsSettingsRequest(string? GatewayUrl, string? ApiKey, string? SenderId, bool Enabled);

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await _sms.GetStatusAsync(ct);
        return Ok(new { enabled = status.Enabled, is_configured = status.IsConfigured, message = status.Message });
    }

    [HttpGet]
    [Authorize(Policy = "Permission:Settings.View")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<SmsSettings>(ignoreTenant: false);
        var s = await _db.QueryFirstOrDefaultAsync<SmsSettings>(
            $"""SELECT * FROM "SmsSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        if (s == null) return Ok(new { enabled = false });

        return Ok(new
        {
            gateway_url = s.GatewayUrl,
            api_key = MaskSecret(s.ApiKeyEncrypted),
            sender_id = s.SenderId,
            enabled = s.Enabled,
            has_api_key = !string.IsNullOrWhiteSpace(s.ApiKeyEncrypted),
        });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> Save([FromBody] SmsSettingsRequest req, CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<SmsSettings>(ignoreTenant: false);
        var s = await _db.QueryFirstOrDefaultAsync<SmsSettings>(
            $"""SELECT * FROM "SmsSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        var isNew = s == null;
        s ??= new SmsSettings();
        if (req.GatewayUrl != null) s.GatewayUrl = req.GatewayUrl;
        if (!string.IsNullOrWhiteSpace(req.ApiKey)) s.ApiKeyEncrypted = req.ApiKey.Trim();
        if (req.SenderId != null) s.SenderId = req.SenderId;
        s.Enabled = req.Enabled;
        if (isNew)
            await _db.InsertAsync(s, ct: ct);
        else
            await _db.UpdateAsync(s, ct: ct);
        return Ok(new { ok = true });
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Length <= 8) return "********";
        return value[..4] + "..." + value[^4..];
    }
}

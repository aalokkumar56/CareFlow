using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/settings/whatsapp")]
public class WhatsAppSettingsController : ControllerBase
{
    private readonly ICureFlowDbSession _db;
    private readonly IWhatsAppSettingsService _settings;

    public WhatsAppSettingsController(ICureFlowDbSession db, IWhatsAppSettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public record WhatsAppSettingsRequest(
        string? Provider,
        string? PhoneNumberId,
        string? WabaId,
        string? AccessToken,
        string? ApiToken,
        string? WhatsBizBaseUrl,
        string? VerifyToken,
        string? AppSecret,
        string? BusinessName,
        bool Enabled);

    [HttpGet("status")]
    [Authorize(Policy = "Permission:WhatsApp.View")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await _settings.GetStatusAsync(ct);
        var runtime = await _settings.GetAsync(ct);
        return Ok(new
        {
            enabled = status.Enabled,
            is_configured = status.IsConfigured,
            message = status.Message,
            provider = runtime.Provider,
        });
    }

    [HttpGet]
    [Authorize(Policy = "Permission:Settings.View")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<WhatsAppSettings>(ignoreTenant: false);
        var settings = await _db.QueryFirstOrDefaultAsync<WhatsAppSettings>(
            $"""SELECT * FROM "WhatsAppSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        if (settings == null)
        {
            return Ok(new
            {
                provider = "MetaCloud",
                enabled = false,
                business_name = "Hospital",
                whats_biz_base_url = "https://whatsbizapi.com/api/wpbox/",
            });
        }

        return Ok(new
        {
            provider = settings.Provider,
            phone_number_id = settings.PhoneNumberId,
            waba_id = settings.WabaId,
            access_token = MaskSecret(settings.AccessTokenEncrypted),
            api_token = MaskSecret(settings.ApiTokenEncrypted),
            whats_biz_base_url = settings.WhatsBizBaseUrl,
            verify_token = settings.VerifyToken,
            app_secret = MaskSecret(settings.AppSecretEncrypted),
            business_name = settings.BusinessName,
            enabled = settings.Enabled,
            has_access_token = !string.IsNullOrWhiteSpace(settings.AccessTokenEncrypted),
            has_api_token = !string.IsNullOrWhiteSpace(settings.ApiTokenEncrypted),
            has_app_secret = !string.IsNullOrWhiteSpace(settings.AppSecretEncrypted),
        });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> Save([FromBody] WhatsAppSettingsRequest req, CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<WhatsAppSettings>(ignoreTenant: false);
        var settings = await _db.QueryFirstOrDefaultAsync<WhatsAppSettings>(
            $"""SELECT * FROM "WhatsAppSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        var isNew = settings == null;
        settings ??= new WhatsAppSettings();

        if (!string.IsNullOrWhiteSpace(req.Provider))
            settings.Provider = req.Provider.Trim();

        if (req.PhoneNumberId != null) settings.PhoneNumberId = req.PhoneNumberId;
        if (req.WabaId != null) settings.WabaId = req.WabaId;
        if (!string.IsNullOrWhiteSpace(req.AccessToken)) settings.AccessTokenEncrypted = req.AccessToken.Trim();
        if (!string.IsNullOrWhiteSpace(req.ApiToken)) settings.ApiTokenEncrypted = req.ApiToken.Trim();
        if (req.WhatsBizBaseUrl != null) settings.WhatsBizBaseUrl = req.WhatsBizBaseUrl;
        if (req.VerifyToken != null) settings.VerifyToken = req.VerifyToken;
        if (!string.IsNullOrWhiteSpace(req.AppSecret)) settings.AppSecretEncrypted = req.AppSecret.Trim();
        if (!string.IsNullOrWhiteSpace(req.BusinessName)) settings.BusinessName = req.BusinessName.Trim();

        settings.Enabled = req.Enabled;
        if (isNew)
            await _db.InsertAsync(settings, ct: ct);
        else
            await _db.UpdateAsync(settings, ct: ct);
        return Ok(new { ok = true });
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Length <= 8) return "********";
        return value[..4] + "..." + value[^4..];
    }
}

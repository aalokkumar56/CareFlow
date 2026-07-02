using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.Integrations;

public class WhatsAppSettingsService : IWhatsAppSettingsService
{
    private const string DefaultWhatsBizBaseUrl = "https://whatsbizapi.com/api/wpbox/";

    private readonly ICureFlowDbSession _db;

    public WhatsAppSettingsService(ICureFlowDbSession db) => _db = db;

    public async Task<WhatsAppRuntimeSettings> GetAsync(CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<WhatsAppSettings>(ignoreTenant: false);
        var s = await _db.QueryFirstOrDefaultAsync<WhatsAppSettings>(
            $"""SELECT * FROM "WhatsAppSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        if (s == null)
        {
            return new WhatsAppRuntimeSettings(
                "MetaCloud", false, false,
                null, null, null, null, DefaultWhatsBizBaseUrl, null, null, "Hospital");
        }

        var provider = string.IsNullOrWhiteSpace(s.Provider) ? "MetaCloud" : s.Provider.Trim();
        var baseUrl = string.IsNullOrWhiteSpace(s.WhatsBizBaseUrl) ? DefaultWhatsBizBaseUrl : s.WhatsBizBaseUrl.Trim();
        var configured = IsConfigured(provider, s);

        return new WhatsAppRuntimeSettings(
            provider,
            s.Enabled,
            configured,
            s.PhoneNumberId,
            s.WabaId,
            s.AccessTokenEncrypted,
            s.ApiTokenEncrypted,
            baseUrl,
            s.VerifyToken,
            s.AppSecretEncrypted,
            s.BusinessName ?? "Hospital");
    }

    public async Task<IntegrationStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await GetAsync(ct);
        if (!settings.IsConfigured)
        {
            return new IntegrationStatusDto(
                settings.Enabled,
                false,
                "WhatsApp is not configured. Add credentials under Settings → Integrations.");
        }

        if (!settings.Enabled)
        {
            return new IntegrationStatusDto(
                false,
                true,
                "WhatsApp is disabled. Enable it under Settings → Integrations to send messages.");
        }

        return new IntegrationStatusDto(true, true, "WhatsApp is active.");
    }

    private static bool IsConfigured(string provider, WhatsAppSettings s)
    {
        if (string.Equals(provider, "WhatsBiz", StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(s.ApiTokenEncrypted);

        return !string.IsNullOrWhiteSpace(s.PhoneNumberId)
            && !string.IsNullOrWhiteSpace(s.AccessTokenEncrypted);
    }
}

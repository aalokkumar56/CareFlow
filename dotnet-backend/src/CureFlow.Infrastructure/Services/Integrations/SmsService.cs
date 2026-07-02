using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services.Integrations;

public class SmsService : ISmsService
{
    private readonly ICureFlowDbSession _db;
    private readonly ILogger<SmsService> _logger;

    public SmsService(ICureFlowDbSession db, ILogger<SmsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IntegrationStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<SmsSettings>(ignoreTenant: false);
        var s = await _db.QueryFirstOrDefaultAsync<SmsSettings>(
            $"""SELECT * FROM "SmsSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        if (s == null || string.IsNullOrWhiteSpace(s.GatewayUrl) || string.IsNullOrWhiteSpace(s.ApiKeyEncrypted))
        {
            return new IntegrationStatusDto(
                s?.Enabled ?? false,
                false,
                "SMS gateway is not configured. Add settings under Settings → Integrations.");
        }

        if (!s.Enabled)
        {
            return new IntegrationStatusDto(
                false,
                true,
                "SMS gateway is disabled. Enable it under Settings → Integrations.");
        }

        return new IntegrationStatusDto(true, true, "SMS gateway is active.");
    }

    public async Task<(bool ok, string? error)> SendAsync(string toPhone, string body, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.Enabled || !status.IsConfigured)
            return (false, status.Message);

        var where = SqlFragments.WhereActive<SmsSettings>(ignoreTenant: false);
        var s = await _db.QueryFirstOrDefaultAsync<SmsSettings>(
            $"""SELECT * FROM "SmsSettings" WHERE {where} LIMIT 1""",
            ct: ct);
        if (s == null)
            return (false, "SMS gateway is not configured.");

        _logger.LogInformation("SMS send queued for {Phone} via {Gateway} (transport not implemented).", toPhone, s.GatewayUrl);
        return (true, null);
    }
}

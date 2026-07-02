using CureFlow.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.Services;

/// <summary>
/// Stub for future Google Calendar sync. No external calls unless sync is enabled and an API key is configured.
/// </summary>
public class GoogleCalendarSyncStub
{
    private readonly MarketingCalendarOptions _options;
    private readonly ILogger<GoogleCalendarSyncStub> _logger;

    public GoogleCalendarSyncStub(IOptions<MarketingCalendarOptions> options, ILogger<GoogleCalendarSyncStub> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SyncIfConfiguredAsync(CancellationToken ct = default)
    {
        if (!_options.GoogleCalendarSyncEnabled)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(_options.GoogleCalendarApiKey))
        {
            _logger.LogDebug("Google Calendar sync enabled but no API key — skipping (stub).");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Google Calendar sync stub: would sync events (not implemented).");
        return Task.CompletedTask;
    }
}

using CureFlow.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.External;

/// <summary>
/// Thin adapter over <see cref="IWhatsappProvider"/> for conversation/campaign outbound flows and webhooks.
/// </summary>
public class WhatsappCloudClient : IWhatsappService
{
    private readonly IWhatsappMessagingService _messaging;
    private readonly IWhatsappProvider _provider;
    private readonly IWhatsappWebhookRelayService _relay;
    private readonly ILogger<WhatsappCloudClient> _logger;

    public WhatsappCloudClient(
        IWhatsappMessagingService messaging,
        IWhatsappProvider provider,
        IWhatsappWebhookRelayService relay,
        ILogger<WhatsappCloudClient> logger)
    {
        _messaging = messaging;
        _provider = provider;
        _relay = relay;
        _logger = logger;
    }

    public Task<(bool ok, string? messageId, string? error)> SendTextAsync(string toPhone, string body, CancellationToken ct = default)
        => _messaging.SendTextAsync(toPhone, body, ct);

    public async Task ProcessIncomingWebhookAsync(string body, string? signatureHeader, CancellationToken ct = default)
    {
        try
        {
            var relayResult = await _relay.ForwardAsync(body, signatureHeader, ct);
            if (relayResult.Attempted && !relayResult.Success)
            {
                _logger.LogWarning(
                    "Webhook relay delivery failed (request {RequestId}): {Error}. Continuing with internal processing.",
                    relayResult.RequestId,
                    relayResult.Error ?? "unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook relay threw an exception. Continuing with internal processing.");
        }

        await _provider.ProcessIncomingWebhookAsync(body, signatureHeader, ct);
    }

    public bool VerifyWebhookSubscription(string mode, string token, out string? challenge, string? challengeParam = null)
        => _provider.VerifyWebhookSubscription(mode, token, out challenge, challengeParam);
}

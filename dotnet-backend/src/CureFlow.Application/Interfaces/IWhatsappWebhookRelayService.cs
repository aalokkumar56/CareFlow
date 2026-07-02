namespace CureFlow.Application.Interfaces;

/// <summary>
/// Forwards normalized WhatsApp webhook payloads to an external relay URL (in addition to internal processing).
/// </summary>
public interface IWhatsappWebhookRelayService
{
    Task<RelayDeliveryResult> ForwardAsync(string rawBody, string? signatureHeader, CancellationToken ct = default);
}

public sealed record RelayDeliveryResult(
    bool Attempted,
    bool Success,
    int Attempts,
    int? StatusCode,
    string? Error,
    string? RequestId);

using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IWhatsappService
{
    Task<(bool ok, string? messageId, string? error)> SendTextAsync(string toPhone, string body, CancellationToken ct = default);
    Task ProcessIncomingWebhookAsync(string body, string? signatureHeader, CancellationToken ct = default);
    bool VerifyWebhookSubscription(string mode, string token, out string? challenge, string? challengeParam = null);
}

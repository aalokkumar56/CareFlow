using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

/// <summary>
/// Centralized outbound WhatsApp transport through <see cref="IWhatsappProvider"/>.
/// Used by campaigns, conversations, and <see cref="IWhatsappService"/>.
/// </summary>
public interface IWhatsappMessagingService
{
    Task<(bool ok, string? messageId, string? error)> SendTextAsync(string toPhone, string body, CancellationToken ct = default);

    Task<(bool ok, string? messageId, string? error)> SendTemplateAsync(SendTemplateMessageRequest request, CancellationToken ct = default);

    /// <summary>CRM batch send: one text per recipient via provider (no duplicate HTTP in callers).</summary>
    Task<IReadOnlyList<CampaignBatchSendResult>> SendCampaignBatchAsync(
        IReadOnlyList<CampaignBatchSendItem> recipients,
        CancellationToken ct = default);
}

public record CampaignBatchSendItem(string Phone, string Body, Guid? PatientId = null);

public record CampaignBatchSendResult(
    string Phone,
    Guid? PatientId,
    bool Success,
    string? MessageId,
    string? Error);

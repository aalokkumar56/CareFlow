using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

/// <summary>
/// Provider abstraction for WhatsApp integrations (WhatsBiz/WPBox, Meta Cloud API).
/// Controllers and business services depend on <see cref="IWhatsappApiService"/> /
/// <see cref="IWhatsappService"/> — not on concrete providers.
/// </summary>
public interface IWhatsappProvider
{
    string ProviderName { get; }

    Task<bool> IsConfiguredAsync(CancellationToken ct = default);

    Task<ProviderSendResult> SendTextAsync(string toPhone, string body, CancellationToken ct = default);

    Task<ProviderSendResult> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken ct = default);

    Task<ProviderSendResult> SendMediaAsync(SendMediaRequest request, CancellationToken ct = default);

    Task<ProviderSendResult> SendMediaFileAsync(
        string phone,
        string mediaType,
        byte[] fileBytes,
        string fileName,
        string contentType,
        string? caption,
        CancellationToken ct = default);

    Task<CampaignTriggerResponse> SendCampaignAsync(SendCampaignRequest request, CancellationToken ct = default);

    Task<WhatsAppApiResponse> MakeContactAsync(MakeContactRequest request, CancellationToken ct = default);

    Task<GetTemplatesResponse> GetTemplatesAsync(bool forceRefresh = false, CancellationToken ct = default);

    Task<GetGroupsResponse> GetGroupsAsync(bool forceRefresh = false, CancellationToken ct = default);

    Task<GetCampaignsResponse> GetCampaignsAsync(string type = "api", bool forceRefresh = false, CancellationToken ct = default);

    Task<GetContactsResponse> GetContactsAsync(bool forceRefresh = false, CancellationToken ct = default);

    bool VerifyWebhookSubscription(string mode, string token, out string? challenge, string? challengeParam = null);

    Task ProcessIncomingWebhookAsync(string body, string? signatureHeader, CancellationToken ct = default);
}

public record ProviderSendResult(bool Success, string? MessageId, string? Error);

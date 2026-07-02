using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.External.Providers;

namespace CureFlow.Infrastructure.External;

/// <summary>Routes outbound WhatsApp calls to WhatsBiz or Meta based on tenant DB settings.</summary>
public class RoutingWhatsappProvider : IWhatsappProvider
{
    private readonly IWhatsAppSettingsService _settings;
    private readonly WhatsBizProvider _whatsBiz;
    private readonly MetaCloudProvider _meta;

    public RoutingWhatsappProvider(
        IWhatsAppSettingsService settings,
        WhatsBizProvider whatsBiz,
        MetaCloudProvider meta)
    {
        _settings = settings;
        _whatsBiz = whatsBiz;
        _meta = meta;
    }

    public string ProviderName => "Routing";

    private async Task<IWhatsappProvider> ResolveAsync(CancellationToken ct)
    {
        var s = await _settings.GetAsync(ct);
        return string.Equals(s.Provider, "WhatsBiz", StringComparison.OrdinalIgnoreCase)
            ? _whatsBiz
            : _meta;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        var s = await _settings.GetAsync(ct);
        return s.Enabled && s.IsConfigured;
    }

    public async Task<ProviderSendResult> SendTextAsync(string toPhone, string body, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).SendTextAsync(toPhone, body, ct);

    public async Task<ProviderSendResult> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).SendTemplateMessageAsync(request, ct);

    public async Task<ProviderSendResult> SendMediaAsync(SendMediaRequest request, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).SendMediaAsync(request, ct);

    public async Task<ProviderSendResult> SendMediaFileAsync(
        string phone, string mediaType, byte[] fileBytes, string fileName, string contentType, string? caption, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).SendMediaFileAsync(phone, mediaType, fileBytes, fileName, contentType, caption, ct);

    public async Task<CampaignTriggerResponse> SendCampaignAsync(SendCampaignRequest request, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).SendCampaignAsync(request, ct);

    public async Task<WhatsAppApiResponse> MakeContactAsync(MakeContactRequest request, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).MakeContactAsync(request, ct);

    public async Task<GetTemplatesResponse> GetTemplatesAsync(bool forceRefresh = false, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).GetTemplatesAsync(forceRefresh, ct);

    public async Task<GetGroupsResponse> GetGroupsAsync(bool forceRefresh = false, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).GetGroupsAsync(forceRefresh, ct);

    public async Task<GetCampaignsResponse> GetCampaignsAsync(string type = "api", bool forceRefresh = false, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).GetCampaignsAsync(type, forceRefresh, ct);

    public async Task<GetContactsResponse> GetContactsAsync(bool forceRefresh = false, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).GetContactsAsync(forceRefresh, ct);

    public async Task ProcessIncomingWebhookAsync(string body, string? signatureHeader, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).ProcessIncomingWebhookAsync(body, signatureHeader, ct);

    public bool VerifyWebhookSubscription(string mode, string token, out string? challenge, string? challengeParam = null)
    {
        // Webhook verification uses verify token from DB; either provider reads DB.
        return _meta.VerifyWebhookSubscription(mode, token, out challenge, challengeParam);
    }
}

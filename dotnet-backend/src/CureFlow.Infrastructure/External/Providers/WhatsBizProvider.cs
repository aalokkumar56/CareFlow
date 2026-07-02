using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.External.Providers;

/// <summary>
/// WhatsBiz / WPBox REST API provider (https://whatsbizapi.com/api/wpbox/).
/// </summary>
public class WhatsBizProvider : IWhatsappProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWhatsAppSettingsService _settingsService;
    private readonly IServiceProvider _services;
    private readonly ILogger<WhatsBizProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WhatsBizProvider(
        IHttpClientFactory httpClientFactory,
        IWhatsAppSettingsService settingsService,
        IServiceProvider services,
        ILogger<WhatsBizProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _services = services;
        _logger = logger;
    }

    public string ProviderName => "WhatsBiz";

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        var s = await _settingsService.GetAsync(ct);
        return s.Enabled && s.IsConfigured
            && string.Equals(s.Provider, "WhatsBiz", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderSendResult> SendTextAsync(string toPhone, string body, CancellationToken ct = default)
    {
        var token = await ResolveApiTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return new ProviderSendResult(false, null, "WhatsBiz API token not configured");

        var phone = WhatsappPhoneHelper.ForApi(toPhone);
        if (string.IsNullOrWhiteSpace(phone))
            return new ProviderSendResult(false, null, "Invalid phone number");

        var payload = new { token, phone, message = body };
        var response = await PostAsync<WhatsBizResponse>("sendmessage", payload, ct);
        return MapSendResult(response);
    }

    public async Task<ProviderSendResult> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken ct = default)
    {
        var token = await ResolveApiTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return new ProviderSendResult(false, null, "WhatsBiz API token not configured");

        var phone = WhatsappPhoneHelper.ForApi(request.Phone);
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(request.TemplateName))
            return new ProviderSendResult(false, null, "Phone and template name are required");

        var payload = new
        {
            token,
            phone,
            template_name = request.TemplateName,
            template_language = string.IsNullOrWhiteSpace(request.TemplateLanguage) ? "en" : request.TemplateLanguage,
            components = request.Components.Select(c => new
            {
                type = c.Type,
                parameters = c.Parameters.Select(p => new
                {
                    type = p.Type,
                    text = p.Text,
                    video = p.Video == null ? null : new { link = p.Video.Link },
                    image = p.Image == null ? null : new { link = p.Image.Link },
                    document = p.Document == null ? null : new { link = p.Document.Link },
                    audio = p.Audio == null ? null : new { link = p.Audio.Link }
                })
            })
        };

        var response = await PostAsync<WhatsBizResponse>("sendtemplatemessage", payload, ct);
        return MapSendResult(response);
    }

    public async Task<ProviderSendResult> SendMediaAsync(SendMediaRequest request, CancellationToken ct = default)
    {
        var token = await ResolveApiTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return new ProviderSendResult(false, null, "WhatsBiz API token not configured");

        var phone = WhatsappPhoneHelper.ForApi(request.Phone);
        var mediaType = request.MediaType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(mediaType) || string.IsNullOrWhiteSpace(request.MediaUrl))
            return new ProviderSendResult(false, null, "Phone, media type, and media URL are required");

        var payload = new
        {
            token,
            phone,
            type = mediaType,
            media_url = request.MediaUrl,
            caption = request.Caption
        };

        var response = await PostAsync<WhatsBizResponse>("sendmedia", payload, ct);
        return MapSendResult(response);
    }

    public Task<ProviderSendResult> SendMediaFileAsync(
        string phone,
        string mediaType,
        byte[] fileBytes,
        string fileName,
        string contentType,
        string? caption,
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "WhatsBiz provider does not support direct file upload. Host the file at a public HTTPS URL and use SendMediaAsync.");
        return Task.FromResult(new ProviderSendResult(
            false,
            null,
            "WhatsBiz requires a public media URL. Direct file upload is only supported with MetaCloud provider."));
    }

    public async Task<CampaignTriggerResponse> SendCampaignAsync(SendCampaignRequest request, CancellationToken ct = default)
    {
        var token = await ResolveApiTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new CampaignTriggerResponse
            {
                Success = false,
                CampaignId = request.CampaignId.ToString(),
                Status = "not_configured",
                TriggeredAt = DateTime.UtcNow
            };
        }

        var payload = new { token, campaign_id = request.CampaignId };
        var response = await PostAsync<JsonElement>("sendcampaigns", payload, ct);
        var success = response.ValueKind == JsonValueKind.Object
            && response.TryGetProperty("status", out var statusEl)
            && string.Equals(statusEl.GetString(), "success", StringComparison.OrdinalIgnoreCase);

        return new CampaignTriggerResponse
        {
            Success = success,
            CampaignId = request.CampaignId.ToString(),
            Status = success ? "triggered" : "failed",
            TriggeredAt = DateTime.UtcNow
        };
    }

    public async Task<WhatsAppApiResponse> MakeContactAsync(MakeContactRequest request, CancellationToken ct = default)
    {
        var token = await ResolveApiTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return new WhatsAppApiResponse { Success = false, Message = "WhatsBiz API token not configured" };

        var phone = WhatsappPhoneHelper.ForApi(request.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            return new WhatsAppApiResponse { Success = false, Message = "Invalid phone number" };

        var payload = new Dictionary<string, object?>
        {
            ["token"] = token,
            ["phone"] = phone
        };

        if (!string.IsNullOrWhiteSpace(request.Name))
            payload["name"] = request.Name;
        if (request.GroupId.HasValue)
            payload["groups"] = new[] { request.GroupId.Value };
        if (request.CustomFields is { Count: > 0 })
            payload["custom"] = request.CustomFields;

        var response = await PostAsync<JsonElement>("makeContact", payload, ct);
        var success = response.ValueKind == JsonValueKind.Object
            && response.TryGetProperty("status", out var statusEl)
            && string.Equals(statusEl.GetString(), "success", StringComparison.OrdinalIgnoreCase);

        return new WhatsAppApiResponse
        {
            Success = success,
            Message = success ? "Contact created" : "Contact creation failed",
            Data = response.ValueKind == JsonValueKind.Undefined ? null : response
        };
    }

    public async Task<GetTemplatesResponse> GetTemplatesAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        var token = await ResolveApiTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return new GetTemplatesResponse { Status = "not_configured", Templates = new() };

        try
        {
            var response = await GetAsync<WhatsBizTemplatesResponse>($"getTemplates?token={Uri.EscapeDataString(token)}", ct);
            if (response?.Templates == null)
                return new GetTemplatesResponse { Status = "error", Templates = new() };

            return new GetTemplatesResponse
            {
                Status = response.Status ?? "success",
                Templates = response.Templates.Select(MapTemplate).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch WhatsBiz templates.");
            return new GetTemplatesResponse { Status = "error", Templates = new() };
        }
    }

    public async Task<GetGroupsResponse> GetGroupsAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        var token = await ResolveApiTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return new GetGroupsResponse { Status = "not_configured", Groups = new() };

        try
        {
            var response = await GetAsync<WhatsBizGroupsResponse>($"getGroups?token={Uri.EscapeDataString(token)}", ct);
            if (response?.Groups == null)
                return new GetGroupsResponse { Status = "error", Groups = new() };

            return new GetGroupsResponse
            {
                Status = response.Status ?? "success",
                Groups = response.Groups.Select(MapGroup).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch WhatsBiz groups.");
            return new GetGroupsResponse { Status = "error", Groups = new() };
        }
    }

    public async Task<GetCampaignsResponse> GetCampaignsAsync(string type = "api", bool forceRefresh = false, CancellationToken ct = default)
    {
        var token = await ResolveApiTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return new GetCampaignsResponse { Status = "not_configured", Campaigns = new() };

        try
        {
            var response = await GetAsync<WhatsBizCampaignsResponse>(
                $"getCampaigns?token={Uri.EscapeDataString(token)}&type={Uri.EscapeDataString(type)}", ct);
            if (response?.Campaigns == null)
                return new GetCampaignsResponse { Status = "error", Campaigns = new() };

            return new GetCampaignsResponse
            {
                Status = response.Status ?? "success",
                Campaigns = response.Campaigns.Select(MapCampaign).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch WhatsBiz campaigns.");
            return new GetCampaignsResponse { Status = "error", Campaigns = new() };
        }
    }

    public async Task<GetContactsResponse> GetContactsAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        var token = await ResolveApiTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return new GetContactsResponse { Status = "not_configured", Contacts = new() };

        try
        {
            var response = await GetAsync<WhatsBizContactsResponse>($"getContacts?token={Uri.EscapeDataString(token)}", ct);
            if (response?.Contacts == null)
                return new GetContactsResponse { Status = "error", Contacts = new() };

            return new GetContactsResponse
            {
                Status = response.Status ?? "success",
                Contacts = response.Contacts.Select(MapContact).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch WhatsBiz contacts.");
            return new GetContactsResponse { Status = "error", Contacts = new() };
        }
    }

    public bool VerifyWebhookSubscription(string mode, string token, out string? challenge, string? challengeParam = null)
    {
        challenge = challengeParam;
        var settings = _settingsService.GetAsync().GetAwaiter().GetResult();
        var expected = settings.VerifyToken;
        return mode == "subscribe" && !string.IsNullOrEmpty(expected) && token == expected;
    }

    public Task ProcessIncomingWebhookAsync(string body, string? signatureHeader, CancellationToken ct = default)
        => ProcessIncomingWebhookCoreAsync(body, signatureHeader, ct);

    private async Task ProcessIncomingWebhookCoreAsync(string body, string? signatureHeader, CancellationToken ct)
    {
        var appSecret = await ResolveAppSecretAsync(ct);
        if (!string.IsNullOrEmpty(appSecret) && !WhatsappWebhookProcessor.VerifyMetaSignature(body, signatureHeader, appSecret))
        {
            _logger.LogWarning("Invalid WhatsApp webhook signature");
            return;
        }

        await WhatsappWebhookProcessor.ProcessAsync(body, ct, _services);
    }

    private async Task<string?> ResolveAppSecretAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetAsync(ct);
        return settings.AppSecret;
    }

    private async Task<string?> ResolveApiTokenAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetAsync(ct);
        return string.IsNullOrWhiteSpace(settings.ApiToken) ? null : settings.ApiToken.Trim();
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("WhatsBiz");

    private async Task<T?> PostAsync<T>(string endpoint, object payload, CancellationToken ct)
    {
        var baseUri = await GetWhatsBizBaseUriAsync(ct);
        if (baseUri == null)
        {
            _logger.LogWarning("WhatsBiz POST {Endpoint} skipped — WhatsBizBaseUrl is not configured.", endpoint);
            return default;
        }

        var client = CreateClient();
        var requestUri = new Uri(baseUri, endpoint);
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(requestUri, content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("WhatsBiz POST {Endpoint} failed: {Status} {Body}", endpoint, response.StatusCode, Truncate(responseContent));
            return default;
        }

        return DeserializeResponse<T>(endpoint, responseContent, "POST");
    }

    private async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct)
    {
        var baseUri = await GetWhatsBizBaseUriAsync(ct);
        if (baseUri == null)
        {
            _logger.LogWarning("WhatsBiz GET {Endpoint} skipped — WhatsBizBaseUrl is not configured.", endpoint);
            return default;
        }

        var client = CreateClient();
        var requestUri = new Uri(baseUri, endpoint.TrimStart('/'));
        var response = await client.GetAsync(requestUri, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("WhatsBiz GET {Endpoint} failed: {Status} {Body}", endpoint, response.StatusCode, responseContent);
            return default;
        }

        return DeserializeResponse<T>(endpoint, responseContent, "GET");
    }

    private T? DeserializeResponse<T>(string endpoint, string responseContent, string method)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            _logger.LogWarning("WhatsBiz {Method} {Endpoint} returned an empty body.", method, endpoint);
            return default;
        }

        var trimmed = responseContent.TrimStart();
        if (trimmed.StartsWith('<') || trimmed.StartsWith("<!"))
        {
            _logger.LogWarning(
                "WhatsBiz {Method} {Endpoint} returned HTML instead of JSON: {Body}",
                method, endpoint, Truncate(responseContent));
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(responseContent, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "WhatsBiz {Method} {Endpoint} returned unexpected JSON: {Body}",
                method, endpoint, Truncate(responseContent));
            return default;
        }
    }

    private static string Truncate(string value, int maxLength = 500)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";

    private async Task<Uri?> GetWhatsBizBaseUriAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.WhatsBizBaseUrl))
            return null;

        var normalized = settings.WhatsBizBaseUrl.TrimEnd('/') + "/";
        return Uri.TryCreate(normalized, UriKind.Absolute, out var baseUri) ? baseUri : null;
    }

    private static ProviderSendResult MapSendResult(WhatsBizResponse? response)
    {
        if (response == null)
            return new ProviderSendResult(false, null, "No response from WhatsBiz API");

        var success = string.Equals(response.Status, "success", StringComparison.OrdinalIgnoreCase);
        var messageId = !string.IsNullOrWhiteSpace(response.Message_Wamid)
            ? response.Message_Wamid
            : response.Message_Id > 0 ? response.Message_Id.ToString() : null;

        return new ProviderSendResult(success, messageId, success ? null : response.Message);
    }

    private static TemplateDTO MapTemplate(WhatsBizTemplateItem item) => new()
    {
        Id = ParseExternalId(item.Id),
        Name = item.Name ?? string.Empty,
        Status = item.Status ?? string.Empty,
        Category = item.Category ?? string.Empty,
        Language = item.Language ?? string.Empty,
        Components = item.Components ?? string.Empty,
        CompanyId = ParseExternalId(item.Company_Id),
        CreatedAt = item.Created_At ?? DateTime.UtcNow,
        UpdatedAt = item.Updated_At ?? DateTime.UtcNow
    };

    private static GroupDTO MapGroup(WhatsBizGroupItem item) => new()
    {
        Id = ParseExternalId(item.Id),
        Name = item.Name ?? string.Empty,
        CompanyId = ParseExternalId(item.Company_Id),
        DeletedAt = item.Deleted_At,
        CreatedAt = item.Created_At ?? DateTime.UtcNow,
        UpdatedAt = item.Updated_At ?? DateTime.UtcNow
    };

    private static CampaignDTO MapCampaign(WhatsBizCampaignItem item) => new()
    {
        Id = ParseExternalId(item.Id),
        Name = item.Name ?? string.Empty,
        Type = item.Type ?? string.Empty,
        CompanyId = ParseExternalId(item.Company_Id),
        DeletedAt = item.Deleted_At,
        CreatedAt = item.Created_At ?? DateTime.UtcNow,
        UpdatedAt = item.Updated_At ?? DateTime.UtcNow
    };

    private static ContactDTO MapContact(WhatsBizContactItem item) => new()
    {
        Id = item.Id?.ToString() ?? string.Empty,
        Phone = item.Phone ?? string.Empty,
        Name = item.Name ?? string.Empty,
        GroupId = item.Group_Id?.ToString(),
        CompanyId = item.Company_Id?.ToString() ?? string.Empty,
        CreatedAt = item.Created_At ?? DateTime.UtcNow,
        UpdatedAt = item.Updated_At ?? DateTime.UtcNow
    };

    private static int ParseExternalId(JsonElement id)
    {
        return id.ValueKind switch
        {
            JsonValueKind.Number when id.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(id.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static int ParseExternalId(JsonElement? id) => id is null ? 0 : ParseExternalId(id.Value);

    private sealed class WhatsBizTemplatesResponse
    {
        public string? Status { get; set; }
        public List<WhatsBizTemplateItem>? Templates { get; set; }
    }

    private sealed class WhatsBizTemplateItem
    {
        public JsonElement Id { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Language { get; set; }
        public string? Components { get; set; }
        public JsonElement Company_Id { get; set; }
        public DateTime? Created_At { get; set; }
        public DateTime? Updated_At { get; set; }
    }

    private sealed class WhatsBizGroupsResponse
    {
        public string? Status { get; set; }
        public List<WhatsBizGroupItem>? Groups { get; set; }
    }

    private sealed class WhatsBizGroupItem
    {
        public JsonElement Id { get; set; }
        public string? Name { get; set; }
        public JsonElement Company_Id { get; set; }
        public DateTime? Deleted_At { get; set; }
        public DateTime? Created_At { get; set; }
        public DateTime? Updated_At { get; set; }
    }

    private sealed class WhatsBizCampaignsResponse
    {
        public string? Status { get; set; }
        public List<WhatsBizCampaignItem>? Campaigns { get; set; }
    }

    private sealed class WhatsBizCampaignItem
    {
        public JsonElement Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public JsonElement Company_Id { get; set; }
        public DateTime? Deleted_At { get; set; }
        public DateTime? Created_At { get; set; }
        public DateTime? Updated_At { get; set; }
    }

    private sealed class WhatsBizContactsResponse
    {
        public string? Status { get; set; }
        public List<WhatsBizContactItem>? Contacts { get; set; }
    }

    private sealed class WhatsBizContactItem
    {
        public int? Id { get; set; }
        public string? Phone { get; set; }
        public string? Name { get; set; }
        public int? Group_Id { get; set; }
        public int? Company_Id { get; set; }
        public DateTime? Created_At { get; set; }
        public DateTime? Updated_At { get; set; }
    }
}

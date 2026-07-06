using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.External.Providers;

/// <summary>
/// Official Meta WhatsApp Cloud API v18 provider.
/// </summary>
public class MetaCloudProvider : IWhatsappProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWhatsAppSettingsService _settingsService;
    private readonly WhatsappOptions _options;
    private readonly IServiceProvider _services;
    private readonly ILogger<MetaCloudProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string GraphApiVersion =>
        string.IsNullOrWhiteSpace(_options.MetaGraphApiVersion) ? "v18.0" : _options.MetaGraphApiVersion.Trim('/');

    private string GraphApiBaseUrl =>
        (_options.MetaGraphApiBaseUrl ?? "https://graph.facebook.com").TrimEnd('/');

    private string GraphResourcePath(string resourcePath) =>
        $"{GraphApiBaseUrl}/{GraphApiVersion}/{resourcePath.TrimStart('/')}";

    public MetaCloudProvider(
        IHttpClientFactory httpClientFactory,
        IWhatsAppSettingsService settingsService,
        IOptions<WhatsappOptions> options,
        IServiceProvider services,
        ILogger<MetaCloudProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _options = options.Value;
        _services = services;
        _logger = logger;
    }

    public string ProviderName => "MetaCloud";

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        var settings = await ResolveSettingsAsync(ct);
        return settings.Enabled
            && !string.IsNullOrWhiteSpace(settings.PhoneNumberId)
            && !string.IsNullOrWhiteSpace(settings.AccessToken);
    }

    public async Task<ProviderSendResult> SendTextAsync(string toPhone, string body, CancellationToken ct = default)
    {
        var settings = await ResolveSettingsAsync(ct);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.PhoneNumberId) || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new ProviderSendResult(false, null, "Meta Cloud API not configured");

        var normalizedPhone = WhatsappPhoneHelper.Normalize(toPhone);
        var url = GraphResourcePath($"{settings.PhoneNumberId}/messages");
        var payload = new
        {
            messaging_product = "whatsapp",
            to = normalizedPhone,
            type = "text",
            text = new { preview_url = false, body }
        };

        var response = await PostGraphAsync<JsonElement>(url, payload, settings.AccessToken!, ct);
        return MapGraphSendResult(response);
    }

    public async Task<ProviderSendResult> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken ct = default)
    {
        var settings = await ResolveSettingsAsync(ct);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.PhoneNumberId) || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new ProviderSendResult(false, null, "Meta Cloud API not configured");

        var normalizedPhone = WhatsappPhoneHelper.Normalize(request.Phone);
        var url = GraphResourcePath($"{settings.PhoneNumberId}/messages");
        var payload = new
        {
            messaging_product = "whatsapp",
            to = normalizedPhone,
            type = "template",
            template = new
            {
                name = request.TemplateName,
                language = new { code = request.TemplateLanguage },
                components = request.Components
            }
        };

        var response = await PostGraphAsync<JsonElement>(url, payload, settings.AccessToken!, ct);
        return MapGraphSendResult(response);
    }

    public async Task<ProviderSendResult> SendMediaAsync(SendMediaRequest request, CancellationToken ct = default)
    {
        var settings = await ResolveSettingsAsync(ct);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.PhoneNumberId) || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new ProviderSendResult(false, null, "Meta Cloud API not configured");

        var normalizedPhone = WhatsappPhoneHelper.Normalize(request.Phone);
        var mediaType = request.MediaType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(mediaType) || string.IsNullOrWhiteSpace(request.MediaUrl))
            return new ProviderSendResult(false, null, "Phone, media type, and media URL are required");

        object? mediaPayload = mediaType switch
        {
            "image" => new { link = request.MediaUrl, caption = request.Caption },
            "video" => new { link = request.MediaUrl, caption = request.Caption },
            "audio" => new { link = request.MediaUrl },
            "document" => new { link = request.MediaUrl, caption = request.Caption },
            _ => null
        };

        if (mediaPayload == null)
            return new ProviderSendResult(false, null, $"Unsupported media type: {mediaType}");

        var url = GraphResourcePath($"{settings.PhoneNumberId}/messages");
        var payload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = normalizedPhone,
            ["type"] = mediaType,
            [mediaType] = mediaPayload
        };

        var response = await PostGraphAsync<JsonElement>(url, payload, settings.AccessToken!, ct);
        return MapGraphSendResult(response);
    }

    public async Task<ProviderSendResult> SendMediaFileAsync(
        string phone,
        string mediaType,
        byte[] fileBytes,
        string fileName,
        string contentType,
        string? caption,
        CancellationToken ct = default)
    {
        var settings = await ResolveSettingsAsync(ct);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.PhoneNumberId) || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new ProviderSendResult(false, null, "Meta Cloud API not configured");

        var mediaId = await UploadMediaFileAsync(settings.PhoneNumberId!, settings.AccessToken!, mediaType, fileBytes, fileName, contentType, ct);
        if (string.IsNullOrWhiteSpace(mediaId))
            return new ProviderSendResult(false, null, "Media upload failed");

        var normalizedPhone = WhatsappPhoneHelper.Normalize(phone);
        object? mediaPayload = mediaType switch
        {
            "image" => new { id = mediaId, caption },
            "video" => new { id = mediaId, caption },
            "audio" => new { id = mediaId },
            "document" => new { id = mediaId, caption },
            _ => null
        };

        if (mediaPayload == null)
            return new ProviderSendResult(false, null, $"Unsupported media type: {mediaType}");

        var url = GraphResourcePath($"{settings.PhoneNumberId}/messages");
        var payload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = normalizedPhone,
            ["type"] = mediaType,
            [mediaType] = mediaPayload
        };

        var response = await PostGraphAsync<JsonElement>(url, payload, settings.AccessToken!, ct);
        return MapGraphSendResult(response);
    }

    public Task<CampaignTriggerResponse> SendCampaignAsync(SendCampaignRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Meta Cloud provider does not support external campaign triggers for campaign {CampaignId}.", request.CampaignId);
        return Task.FromResult(new CampaignTriggerResponse
        {
            Success = false,
            CampaignId = request.CampaignId.ToString(),
            Status = "unsupported",
            TriggeredAt = DateTime.UtcNow
        });
    }

    public async Task<WhatsAppApiResponse> MakeContactAsync(MakeContactRequest request, CancellationToken ct = default)
    {
        var settings = await ResolveSettingsAsync(ct);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.PhoneNumberId) || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new WhatsAppApiResponse { Success = false, Message = "Meta Cloud API not configured" };

        var normalizedPhone = WhatsappPhoneHelper.Normalize(request.Phone);
        var url = GraphResourcePath($"{settings.PhoneNumberId}/contacts");
        var payload = new
        {
            blocking = "wait",
            contacts = new[] { new { input = normalizedPhone } }
        };

        var result = await PostGraphAsync<JsonElement>(url, payload, settings.AccessToken!, ct);
        var success = result.ValueKind != JsonValueKind.Undefined;
        return new WhatsAppApiResponse
        {
            Success = success,
            Message = success ? "Contact validated" : "Contact validation failed",
            Data = result.ValueKind == JsonValueKind.Undefined ? null : result
        };
    }

    public Task<GetTemplatesResponse> GetTemplatesAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        _logger.LogInformation("Meta Cloud template listing via Graph API is not implemented in this provider.");
        return Task.FromResult(new GetTemplatesResponse { Status = "unsupported", Templates = new() });
    }

    public Task<GetGroupsResponse> GetGroupsAsync(bool forceRefresh = false, CancellationToken ct = default)
        => Task.FromResult(new GetGroupsResponse { Status = "unsupported", Groups = new() });

    public Task<GetCampaignsResponse> GetCampaignsAsync(string type = "api", bool forceRefresh = false, CancellationToken ct = default)
        => Task.FromResult(new GetCampaignsResponse { Status = "unsupported", Campaigns = new() });

    public Task<GetContactsResponse> GetContactsAsync(bool forceRefresh = false, CancellationToken ct = default)
        => Task.FromResult(new GetContactsResponse { Status = "unsupported", Contacts = new() });

    public bool VerifyWebhookSubscription(string mode, string token, out string? challenge, string? challengeParam = null)
    {
        challenge = challengeParam;
        var settings = _settingsService.GetForWebhookAsync().GetAwaiter().GetResult();
        var expected = settings.VerifyToken;
        return mode == "subscribe" && !string.IsNullOrEmpty(expected) && token == expected;
    }

    public Task ProcessIncomingWebhookAsync(string body, string? signatureHeader, CancellationToken ct = default)
        => WhatsappWebhookProcessor.ProcessAsync(body, signatureHeader, ct, _services);

    private async Task<(string? PhoneNumberId, string? AccessToken, string? AppSecret, bool Enabled)> ResolveSettingsAsync(CancellationToken ct)
    {
        var s = await _settingsService.GetAsync(ct);
        return (s.PhoneNumberId, s.AccessToken, s.AppSecret, s.Enabled);
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("MetaCloud");

    private async Task<T?> PostGraphAsync<T>(string url, object payload, string accessToken, CancellationToken ct)
    {
        var client = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(req, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Meta Graph API POST failed: {Status} {Body}", response.StatusCode, responseContent);
            return default;
        }

        return JsonSerializer.Deserialize<T>(responseContent, JsonOptions);
    }

    private async Task<string?> UploadMediaFileAsync(
        string phoneNumberId,
        string accessToken,
        string mediaType,
        byte[] fileBytes,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        var endpoint = GraphResourcePath($"{phoneNumberId}/media");
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent("whatsapp"), "messaging_product");
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        multipart.Add(fileContent, "file", fileName);

        var response = await client.PostAsync(endpoint, multipart, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Meta media upload failed: {Status} {Body}", response.StatusCode, responseContent);
            return null;
        }

        var payload = JsonSerializer.Deserialize<JsonElement>(responseContent, JsonOptions);
        return payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("id", out var idEl)
            ? idEl.GetString()
            : null;
    }

    private static ProviderSendResult MapGraphSendResult(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object)
            return new ProviderSendResult(false, null, "No response from Meta Graph API");

        if (response.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0)
        {
            var msgId = messages[0].GetProperty("id").GetString();
            return new ProviderSendResult(!string.IsNullOrWhiteSpace(msgId), msgId, null);
        }

        var error = response.TryGetProperty("error", out var errorEl)
            ? errorEl.GetProperty("message").GetString()
            : "Meta Graph API send failed";

        return new ProviderSendResult(false, null, error);
    }
}

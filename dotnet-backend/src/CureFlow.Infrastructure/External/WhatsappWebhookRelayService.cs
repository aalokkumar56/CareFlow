using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.External;

/// <summary>
/// Normalizes inbound WhatsApp webhook payloads and forwards them to a configured external relay URL.
/// Internal inbox processing is unaffected — this runs as an additional forward step.
/// </summary>
public sealed class WhatsappWebhookRelayService : IWhatsappWebhookRelayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WhatsappOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<WhatsappWebhookRelayService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public WhatsappWebhookRelayService(IHttpClientFactory httpClientFactory, IOptions<WhatsappOptions> options, IHostEnvironment environment, ILogger<WhatsappWebhookRelayService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<RelayDeliveryResult> ForwardAsync(string rawBody, string? signatureHeader, CancellationToken ct = default)
    {
        if (!_options.IsRelayConfigured)
        {
            _logger.LogDebug("Webhook relay skipped — RelayWebhookUrl not configured or relay disabled.");
            return new RelayDeliveryResult(false, true, 0, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogWarning("Webhook relay skipped — empty request body.");
            return new RelayDeliveryResult(true, false, 0, null, "Empty body", null);
        }

        var requestId = Guid.NewGuid().ToString("N");
        JsonNode? originalNode;
        try
        {
            originalNode = JsonNode.Parse(rawBody);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Webhook relay skipped — invalid JSON (request {RequestId}).", requestId);
            return new RelayDeliveryResult(true, false, 0, null, "Invalid JSON", requestId);
        }

        if (originalNode == null)
            return new RelayDeliveryResult(true, false, 0, null, "Null JSON root", requestId);

        var normalized = await BuildNormalizedPayloadAsync(originalNode, rawBody, requestId, ct);
        var payloadJson = normalized.ToJsonString(JsonOptions);

        _logger.LogInformation(
            "Webhook relay forwarding request {RequestId} to {Url} ({Bytes} bytes, event={EventType})",
            requestId,
            _options.RelayWebhookUrl,
            payloadJson.Length,
            normalized["event"]?["type"]?.GetValue<string>() ?? "unknown");

        var maxAttempts = Math.Max(1, _options.RelayWebhookRetryCount);
        var delayMs = Math.Max(0, _options.RelayWebhookRetryDelayMs);
        HttpStatusCode? lastStatus = null;
        string? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var client = _httpClientFactory.CreateClient("WebhookRelay");
                using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                content.Headers.Add("X-CureFlow-Request-Id", requestId);
                content.Headers.Add("X-CureFlow-Webhook-Format", "normalized-v1");
                if (!string.IsNullOrWhiteSpace(signatureHeader))
                    content.Headers.Add("X-Hub-Signature-256", signatureHeader);

                using var response = await client.PostAsync(_options.RelayWebhookUrl, content, ct);
                lastStatus = response.StatusCode;
                var responseBody = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Webhook relay delivered request {RequestId} on attempt {Attempt}/{MaxAttempts} — HTTP {Status}",
                        requestId, attempt, maxAttempts, (int)response.StatusCode);
                    return new RelayDeliveryResult(true, true, attempt, (int)response.StatusCode, null, requestId);
                }

                lastError = $"HTTP {(int)response.StatusCode}: {Truncate(responseBody)}";
                _logger.LogWarning(
                    "Webhook relay attempt {Attempt}/{MaxAttempts} failed for request {RequestId}: {Error}",
                    attempt, maxAttempts, requestId, lastError);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastError = ex.Message;
                _logger.LogWarning(
                    ex,
                    "Webhook relay attempt {Attempt}/{MaxAttempts} threw for request {RequestId}",
                    attempt, maxAttempts, requestId);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogError(
                    ex,
                    "Webhook relay failed after {MaxAttempts} attempts for request {RequestId}",
                    maxAttempts, requestId);
                return new RelayDeliveryResult(true, false, attempt, (int?)lastStatus, lastError, requestId);
            }

            if (attempt < maxAttempts && delayMs > 0)
                await Task.Delay(delayMs, ct);
        }

        return new RelayDeliveryResult(true, false, maxAttempts, (int?)lastStatus, lastError, requestId);
    }
    private async Task<JsonObject> BuildNormalizedPayloadAsync(JsonNode originalNode, string rawBody, string requestId, CancellationToken ct)
    {
        var receivedAt = DateTime.UtcNow;
        var messages = new JsonArray();
        var statuses = new JsonArray();
        var eventType = "whatsapp.webhook.received";

        CollectFromNode(originalNode, messages, statuses);

        if (messages.Count == 0 && statuses.Count == 0 && originalNode is JsonObject rootObj)
        {
            var flatMessage = TryBuildFlatMessage(rootObj);
            if (flatMessage != null)
                messages.Add(flatMessage);
        }

        if (statuses.Count > 0 && messages.Count == 0)
            eventType = "whatsapp.status.updated";
        else if (messages.Count > 0)
            eventType = "whatsapp.message.received";

        for (var i = 0; i < messages.Count; i++)
        {
            if (messages[i] is JsonObject msgObj)
                await EnrichMediaAsync(msgObj, ct);
        }

        return new JsonObject
        {
            ["event"] = new JsonObject
            {
                ["id"] = $"evt_{receivedAt:yyyyMMddHHmmssfff}_{requestId[..8]}",
                ["type"] = eventType,
                ["timestamp"] = receivedAt.ToString("O"),
                ["version"] = "1.0"
            },
            ["data"] = new JsonObject
            {
                ["messages"] = messages,
                ["statuses"] = statuses
            },
            ["source"] = new JsonObject
            {
                ["provider"] = _options.Provider,
                ["phone_number_id"] = _options.PhoneNumberId
            },
            ["whatsapp"] = new JsonObject
            {
                ["original_payload"] = JsonNode.Parse(rawBody)
            },
            ["metadata"] = new JsonObject
            {
                ["source"] = "cureflow_webhook_relay",
                ["request_id"] = requestId,
                ["received_at"] = receivedAt.ToString("O"),
                ["environment"] = _environment.EnvironmentName
            }
        };
    }
    private static void CollectFromNode(JsonNode node, JsonArray messages, JsonArray statuses)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("entry", out var entryNode) && entryNode is JsonArray entries)
            {
                foreach (var entry in entries)
                    CollectMetaEntry(entry, messages, statuses);
                return;
            }

            if (obj.TryGetPropertyValue("messages", out var msgNode))
                AppendMessages(msgNode, messages);

            if (obj.TryGetPropertyValue("statuses", out var statusNode))
                AppendStatuses(statusNode, statuses);

            if (obj.TryGetPropertyValue("data", out var dataNode) && dataNode != null)
                CollectFromNode(dataNode, messages, statuses);

            if (obj.TryGetPropertyValue("event", out var eventNode) && eventNode != null)
                CollectFromNode(eventNode, messages, statuses);

            if (obj.TryGetPropertyValue("payload", out var payloadNode) && payloadNode != null)
                CollectFromNode(payloadNode, messages, statuses);
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is JsonObject itemObj
                    && (itemObj.ContainsKey("from") || itemObj.ContainsKey("phone") || itemObj.ContainsKey("phone_number")))
                {
                    messages.Add(NormalizeMessage(itemObj));
                }
                else
                {
                    CollectFromNode(item!, messages, statuses);
                }
            }
        }
    }
    private static void CollectMetaEntry(JsonNode? entry, JsonArray messages, JsonArray statuses)
    {
        if (entry is not JsonObject entryObj) return;
        if (!entryObj.TryGetPropertyValue("changes", out var changesNode) || changesNode is not JsonArray changes)
            return;

        foreach (var change in changes)
        {
            if (change is not JsonObject changeObj) continue;
            if (!changeObj.TryGetPropertyValue("value", out var valueNode) || valueNode is not JsonObject valueObj)
                continue;

            if (valueObj.TryGetPropertyValue("messages", out var msgNode))
                AppendMessages(msgNode, messages);
            if (valueObj.TryGetPropertyValue("statuses", out var statusNode))
                AppendStatuses(statusNode, statuses);
        }
    }
    private static void AppendMessages(JsonNode? node, JsonArray target)
    {
        if (node is not JsonArray arr) return;
        foreach (var item in arr)
        {
            if (item is JsonObject obj)
                target.Add(NormalizeMessage(obj));
        }
    }
    private static void AppendStatuses(JsonNode? node, JsonArray target)
    {
        if (node is not JsonArray arr) return;
        foreach (var item in arr)
        {
            if (item is JsonObject obj)
                target.Add(NormalizeStatus(obj));
        }
    }
    private static JsonObject? TryBuildFlatMessage(JsonObject root)
    {
        var phone = ReadString(root, "from", "phone", "phone_number", "phoneNumber", "sender", "wa_phone", "waPhone");
        var text = ReadTextBody(root);
        if (string.IsNullOrWhiteSpace(phone) && root.TryGetPropertyValue("last_message_of_user", out var lastUser))
        {
            phone = ReadString(root, "phone_number", "phoneNumber", "phone");
            text = lastUser?.GetValue<string>();
        }

        if (string.IsNullOrWhiteSpace(phone))
            return null;

        if (IsFromBusiness(root))
            return null;

        return NormalizeMessage(root);
    }
    private static JsonObject NormalizeMessage(JsonObject source)
    {
        var type = ReadString(source, "type") ?? "text";
        var media = ExtractMediaReference(source, type);

        return new JsonObject
        {
            ["id"] = ReadString(source, "id", "message_id", "messageId", "wamid", "wa_message_id"),
            ["from"] = WhatsappPhoneHelper.Normalize(ReadString(source, "from", "phone", "phone_number", "phoneNumber", "sender", "wa_phone", "waPhone") ?? string.Empty),
            ["timestamp"] = ReadString(source, "timestamp", "created_at", "createdAt"),
            ["type"] = type,
            ["body"] = ReadTextBody(source),
            ["from_me"] = IsFromBusiness(source),
            ["media"] = media
        };
    }
    private static JsonObject NormalizeStatus(JsonObject source) => new()
    {
        ["id"] = ReadString(source, "id", "message_id", "messageId", "wamid"),
        ["status"] = ReadString(source, "status"),
        ["timestamp"] = ReadString(source, "timestamp"),
        ["recipient_id"] = ReadString(source, "recipient_id", "recipientId", "to")
    };
    private static JsonObject? ExtractMediaReference(JsonObject source, string type)
    {
        var mediaType = type.ToLowerInvariant();
        if (mediaType is not ("image" or "video" or "audio" or "document" or "sticker"))
        {
            var directUrl = ReadString(source, "media_url", "mediaUrl", "url", "link");
            if (string.IsNullOrWhiteSpace(directUrl))
                return null;
            mediaType = GuessMediaTypeFromUrl(directUrl) ?? "document";
        }

        string? mediaId = null;
        string? mimeType = null;
        string? filename = null;
        string? url = ReadString(source, "media_url", "mediaUrl", "url", "link");

        if (source.TryGetPropertyValue(mediaType, out var typedNode) && typedNode is JsonObject typedObj)
        {
            mediaId ??= ReadString(typedObj, "id");
            mimeType = ReadString(typedObj, "mime_type", "mimeType");
            filename = ReadString(typedObj, "filename", "file_name", "fileName");
            url ??= ReadString(typedObj, "link", "url");
        }

        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(mediaId))
            return null;

        return new JsonObject
        {
            ["type"] = mediaType,
            ["id"] = mediaId,
            ["url"] = url,
            ["mime_type"] = mimeType,
            ["filename"] = filename
        };
    }
    private async Task EnrichMediaAsync(JsonObject message, CancellationToken ct)
    {
        if (message["media"] is not JsonObject media)
            return;

        var existingUrl = media["url"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(existingUrl) && Uri.TryCreate(existingUrl, UriKind.Absolute, out _))
        {
            media["hosted_url"] = existingUrl;
            return;
        }

        var mediaId = media["id"]?.GetValue<string>();
        var downloadUrl = existingUrl;

        if (string.IsNullOrWhiteSpace(downloadUrl) && !string.IsNullOrWhiteSpace(mediaId))
            downloadUrl = await ResolveMetaMediaUrlAsync(mediaId, ct);

        if (string.IsNullOrWhiteSpace(downloadUrl))
            return;

        var hosted = await DownloadAndHostMediaAsync(downloadUrl, media, ct);
        if (!string.IsNullOrWhiteSpace(hosted))
            media["hosted_url"] = hosted;
    }
    private async Task<string?> ResolveMetaMediaUrlAsync(string mediaId, CancellationToken ct)
    {
        var token = _options.AccessToken;
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var baseUrl = (_options.MetaGraphApiBaseUrl ?? "https://graph.facebook.com").TrimEnd('/');
        var version = string.IsNullOrWhiteSpace(_options.MetaGraphApiVersion) ? "v18.0" : _options.MetaGraphApiVersion.Trim('/');
        var url = $"{baseUrl}/{version}/{mediaId}";

        try
        {
            var client = _httpClientFactory.CreateClient("WebhookRelay");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("url", out var urlEl))
                return urlEl.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Meta media URL for id {MediaId}", mediaId);
        }

        return null;
    }
    private async Task<string?> DownloadAndHostMediaAsync(string downloadUrl, JsonObject media, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("WebhookRelay");
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

            if (downloadUrl.Contains("graph.facebook.com", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(_options.AccessToken))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);
            }

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Media download failed HTTP {Status} from {Url}", (int)response.StatusCode, downloadUrl);
                return downloadUrl;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0)
                return downloadUrl;

            var mimeType = media["mime_type"]?.GetValue<string>()
                ?? response.Content.Headers.ContentType?.MediaType
                ?? "application/octet-stream";
            var ext = GuessExtension(mimeType, media["filename"]?.GetValue<string>());
            var fileName = $"{Guid.NewGuid():N}{ext}";

            var storageDir = Path.Combine(_environment.ContentRootPath, _options.RelayMediaStoragePath);
            Directory.CreateDirectory(storageDir);
            var filePath = Path.Combine(storageDir, fileName);
            await File.WriteAllBytesAsync(filePath, bytes, ct);

            var publicBase = (_options.RelayPublicBaseUrl ?? string.Empty).TrimEnd('/');
            var apiPath = $"/api/whatsapp/media/{fileName}";
            var hostedUrl = string.IsNullOrWhiteSpace(publicBase)
                ? apiPath
                : $"{publicBase}{apiPath}";

            media["url"] = hostedUrl;
            media["mime_type"] = mimeType;
            media["size_bytes"] = bytes.Length;

            _logger.LogInformation("Hosted webhook media at {Url} ({Bytes} bytes)", hostedUrl, bytes.Length);
            return hostedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download/host media from {Url}; passing through original URL", downloadUrl);
            return downloadUrl;
        }
    }
    private static bool IsFromBusiness(JsonObject element)
    {
        if (element.TryGetPropertyValue("from_me", out var fromMe) && IsTruthyJson(fromMe))
            return true;
        if (element.TryGetPropertyValue("fromMe", out var fromMeCamel) && IsTruthyJson(fromMeCamel))
            return true;
        if (ReadString(element, "type")?.Equals("bot", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        var dir = ReadString(element, "direction")?.ToLowerInvariant();
        return dir is "outbound" or "outgoing" or "sent";
    }
    private static bool IsTruthyJson(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolVal))
                return boolVal;
            if (value.TryGetValue<int>(out var intVal))
                return intVal == 1;
            if (value.TryGetValue<string>(out var text))
                return text is "1" or "true";
        }
        return false;
    }
    private static string ReadTextBody(JsonObject element)
    {
        if (element.TryGetPropertyValue("text", out var textEl))
        {
            if (textEl is JsonObject textObj && textObj.TryGetPropertyValue("body", out var bodyEl))
                return bodyEl?.GetValue<string>() ?? string.Empty;
            if (textEl is JsonValue textVal)
                return textVal.GetValue<string>() ?? string.Empty;
        }

        return ReadString(element, "message", "body", "content", "last_message_of_user", "lastMessageOfUser") ?? string.Empty;
    }
    private static string? ReadString(JsonObject element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetPropertyValue(name, out var value) || value is not JsonValue jsonValue)
                continue;
            if (jsonValue.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                return text;
        }
        return null;
    }
    private static string? GuessMediaTypeFromUrl(string url)
    {
        var lower = url.ToLowerInvariant();
        if (lower.Contains(".jpg") || lower.Contains(".jpeg") || lower.Contains(".png") || lower.Contains(".webp"))
            return "image";
        if (lower.Contains(".mp4") || lower.Contains(".3gp"))
            return "video";
        if (lower.Contains(".mp3") || lower.Contains(".ogg"))
            return "audio";
        if (lower.Contains(".pdf") || lower.Contains(".doc"))
            return "document";
        return null;
    }
    private static string GuessExtension(string mimeType, string? filename)
    {
        if (!string.IsNullOrWhiteSpace(filename))
        {
            var ext = Path.GetExtension(filename);
            if (!string.IsNullOrWhiteSpace(ext))
                return ext;
        }

        return mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "video/mp4" => ".mp4",
            "audio/ogg" => ".ogg",
            "audio/mpeg" => ".mp3",
            "application/pdf" => ".pdf",
            _ => ".bin"
        };
    }
    private static string Truncate(string value, int max = 300)
        => value.Length <= max ? value : value[..max] + "...";
}

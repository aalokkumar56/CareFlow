using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.External;

/// <summary>
/// File-system backed store for WhatsApp conversation media. Files are written under
/// <c>{ContentRoot}/{StoragePath}/{tenantId}/{messageId}{ext}</c> and served via the
/// authenticated conversation media endpoint.
/// </summary>
public sealed class WhatsappMediaStore : IWhatsappMediaStore
{
    private readonly IHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWhatsAppSettingsService _settingsService;
    private readonly WhatsappMediaOptions _mediaOptions;
    private readonly WhatsappOptions _whatsappOptions;
    private readonly ILogger<WhatsappMediaStore> _logger;

    public WhatsappMediaStore(
        IHostEnvironment environment,
        IHttpClientFactory httpClientFactory,
        IWhatsAppSettingsService settingsService,
        IOptions<WhatsappMediaOptions> mediaOptions,
        IOptions<WhatsappOptions> whatsappOptions,
        ILogger<WhatsappMediaStore> logger)
    {
        _environment = environment;
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _mediaOptions = mediaOptions.Value;
        _whatsappOptions = whatsappOptions.Value;
        _logger = logger;
    }

    public async Task<StoredMedia> SaveOutboundAsync(
        Guid tenantId,
        Guid messageId,
        byte[] content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        if (content == null || content.Length == 0)
            throw new ValidationException("The selected file is empty.");

        var kind = WhatsappMediaPolicy.ClassifyByMime(contentType, _mediaOptions)
            ?? throw new ValidationException($"Unsupported file type '{contentType}'.");

        var max = WhatsappMediaPolicy.MaxBytesFor(kind, _mediaOptions);
        if (content.LongLength > max)
            throw new ValidationException($"File is too large. Maximum size for {WhatsappMediaPolicy.ToTypeString(kind)} is {WhatsappMediaPolicy.FormatBytes(max)}.");

        var safeName = SanitizeFileName(fileName, contentType);
        var relativePath = await WriteFileAsync(tenantId, messageId, content, safeName, contentType, ct);
        return new StoredMedia(relativePath, safeName, contentType, content.LongLength, kind);
    }

    public async Task<StoredMedia?> IngestInboundAsync(
        Guid tenantId,
        Guid messageId,
        InboundMediaReference media,
        CancellationToken ct = default)
    {
        // A URL we resolve ourselves via the configured Meta Graph base URL (from a media id)
        // is trusted; a URL supplied directly in the webhook payload is attacker-influenced.
        var downloadUrl = media.Url;
        var trustedSource = false;
        if (string.IsNullOrWhiteSpace(downloadUrl) && !string.IsNullOrWhiteSpace(media.MediaId))
        {
            downloadUrl = await ResolveMetaMediaUrlAsync(media.MediaId!, ct);
            trustedSource = true;
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            _logger.LogWarning("Inbound media for message {MessageId} had no resolvable URL or media id.", messageId);
            return null;
        }

        // Resolve the request URI, applying SSRF validation to untrusted (webhook-supplied) URLs.
        Uri requestUri;
        if (trustedSource)
        {
            if (!SafeRemoteUrl.TryGetHttpsUri(downloadUrl, out var trustedUri) || trustedUri is null)
            {
                _logger.LogWarning("Inbound media for message {MessageId}: resolved Meta media URL was not https; skipping.", messageId);
                return null;
            }
            requestUri = trustedUri;
        }
        else
        {
            var validation = await SafeRemoteUrl.ValidatePublicHttpsAsync(downloadUrl, Dns.GetHostAddressesAsync, ct);
            if (!validation.IsAllowed || validation.Uri is null)
            {
                _logger.LogWarning("Inbound media for message {MessageId} rejected by URL safety check: {Reason}", messageId, validation.Reason);
                return null;
            }
            requestUri = validation.Uri;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("WebhookRelay");
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (requestUri.Host.Contains("graph.facebook.com", StringComparison.OrdinalIgnoreCase))
            {
                var token = await ResolveAccessTokenAsync(ct);
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Inbound media download failed HTTP {Status} from {Url}", (int)response.StatusCode, requestUri);
                return null;
            }

            // Cap inbound media at the largest configured limit to prevent disk/memory abuse.
            var cap = Math.Max(
                Math.Max(_mediaOptions.MaxImageBytes, _mediaOptions.MaxVideoBytes),
                Math.Max(_mediaOptions.MaxAudioBytes, _mediaOptions.MaxDocumentBytes));

            // Reject oversized payloads from the advertised length before reading the body.
            var declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength.HasValue && declaredLength.Value > cap)
            {
                _logger.LogWarning("Inbound media for message {MessageId} advertises {Size} bytes (> cap); skipping.", messageId, declaredLength.Value);
                return null;
            }

            // Stream with a running byte counter so a chunked/Content-Length-less response
            // cannot buffer the whole body past the cap.
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            var bytes = await BoundedStreamReader.ReadAllWithCapAsync(contentStream, cap, ct);
            if (bytes is null)
            {
                _logger.LogWarning("Inbound media for message {MessageId} exceeds cap ({Cap} bytes) while streaming; skipping.", messageId, cap);
                return null;
            }

            if (bytes.Length == 0)
                return null;

            var contentType = media.MimeType
                ?? response.Content.Headers.ContentType?.ToString()
                ?? "application/octet-stream";

            var kind = WhatsappMediaPolicy.ClassifyByType(media.Type)
                ?? WhatsappMediaPolicy.ClassifyByMime(contentType, _mediaOptions)
                ?? WhatsappMediaKind.Document;

            var safeName = SanitizeFileName(media.FileName, contentType);
            var relativePath = await WriteFileAsync(tenantId, messageId, bytes, safeName, contentType, ct);
            return new StoredMedia(relativePath, safeName, contentType, bytes.LongLength, kind);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ingest inbound media for message {MessageId} from {Url}", messageId, downloadUrl);
            return null;
        }
    }

    public string? ResolveExistingPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var storageRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, StorageRoot()))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var absolute = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, normalized));

        // Guard against path traversal — the resolved file must live inside the storage root.
        if (!absolute.StartsWith(storageRoot, StringComparison.Ordinal))
            return null;

        return File.Exists(absolute) ? absolute : null;
    }

    private async Task<string> WriteFileAsync(
        Guid tenantId,
        Guid messageId,
        byte[] content,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        var storageRoot = StorageRoot();
        var tenantSegment = tenantId == Guid.Empty ? "shared" : tenantId.ToString("N");
        var relativeDir = $"{storageRoot}/{tenantSegment}";
        var absoluteDir = Path.Combine(_environment.ContentRootPath, storageRoot, tenantSegment);
        Directory.CreateDirectory(absoluteDir);

        var ext = ResolveExtension(fileName, contentType);
        var storedName = $"{messageId:N}{ext}";
        var absolutePath = Path.Combine(absoluteDir, storedName);
        await File.WriteAllBytesAsync(absolutePath, content, ct);

        return $"{relativeDir}/{storedName}";
    }

    private string StorageRoot() => string.IsNullOrWhiteSpace(_mediaOptions.StoragePath)
        ? "whatsapp-media"
        : _mediaOptions.StoragePath.Trim('/').Replace('\\', '/');

    private async Task<string?> ResolveAccessTokenAsync(CancellationToken ct)
    {
        try
        {
            var settings = await _settingsService.GetAsync(ct);
            if (!string.IsNullOrWhiteSpace(settings.AccessToken))
                return settings.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve WhatsApp settings for media access token; falling back to options.");
        }

        return _whatsappOptions.AccessToken;
    }

    private async Task<string?> ResolveMetaMediaUrlAsync(string mediaId, CancellationToken ct)
    {
        var token = await ResolveAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var baseUrl = (_whatsappOptions.MetaGraphApiBaseUrl ?? "https://graph.facebook.com").TrimEnd('/');
        var version = string.IsNullOrWhiteSpace(_whatsappOptions.MetaGraphApiVersion) ? "v18.0" : _whatsappOptions.MetaGraphApiVersion.Trim('/');
        var url = $"{baseUrl}/{version}/{mediaId}";

        try
        {
            var client = _httpClientFactory.CreateClient("WebhookRelay");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

    private static string SanitizeFileName(string? fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return $"attachment{ExtensionForMime(contentType)}";

        var name = Path.GetFileName(fileName.Trim());
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        if (string.IsNullOrWhiteSpace(name))
            return $"attachment{ExtensionForMime(contentType)}";

        return name.Length > 180 ? name[^180..] : name;
    }

    private static string ResolveExtension(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(ext))
            return ext.ToLowerInvariant();
        return ExtensionForMime(contentType);
    }

    private static string ExtensionForMime(string contentType)
    {
        var mime = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return mime switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "video/mp4" => ".mp4",
            "video/3gpp" => ".3gp",
            "audio/aac" => ".aac",
            "audio/mp4" => ".m4a",
            "audio/mpeg" => ".mp3",
            "audio/amr" => ".amr",
            "audio/ogg" => ".ogg",
            "application/pdf" => ".pdf",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.ms-powerpoint" => ".ppt",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "text/plain" => ".txt",
            "text/csv" => ".csv",
            _ => ".bin",
        };
    }
}

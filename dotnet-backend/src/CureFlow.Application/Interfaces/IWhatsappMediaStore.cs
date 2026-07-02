using CureFlow.Application.Common;

namespace CureFlow.Application.Interfaces;

/// <summary>
/// Persists and resolves WhatsApp conversation media on the configured storage backend.
/// </summary>
public interface IWhatsappMediaStore
{
    /// <summary>
    /// Persists an outbound attachment to storage. Validation (type/size) is expected to have
    /// already passed; this performs a final size guard and writes the file.
    /// </summary>
    Task<StoredMedia> SaveOutboundAsync(
        Guid tenantId,
        Guid messageId,
        byte[] content,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads inbound media referenced by a webhook payload (resolving Meta media ids when
    /// needed) and persists it to storage. Returns null when the media could not be retrieved.
    /// </summary>
    Task<StoredMedia?> IngestInboundAsync(
        Guid tenantId,
        Guid messageId,
        InboundMediaReference media,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves the absolute path of a stored media file from its relative path, returning null
    /// when the path is empty, escapes the storage root, or does not exist.
    /// </summary>
    string? ResolveExistingPath(string? relativePath);
}

/// <summary>Result of persisting a media file to storage.</summary>
public sealed record StoredMedia(
    string RelativePath,
    string FileName,
    string ContentType,
    long Size,
    WhatsappMediaKind Kind);

/// <summary>A media reference extracted from an inbound webhook payload.</summary>
public sealed record InboundMediaReference(
    string Type,
    string? MediaId,
    string? Url,
    string? MimeType,
    string? FileName,
    string? Caption);

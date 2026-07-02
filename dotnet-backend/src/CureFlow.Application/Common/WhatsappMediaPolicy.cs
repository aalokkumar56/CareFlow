using CureFlow.Application.Options;

namespace CureFlow.Application.Common;

public enum WhatsappMediaKind
{
    Image,
    Video,
    Audio,
    Document,
}

/// <summary>
/// Pure helpers for classifying and validating WhatsApp media. No I/O — safe to unit test.
/// </summary>
public static class WhatsappMediaPolicy
{
    public static string ToTypeString(WhatsappMediaKind kind) => kind switch
    {
        WhatsappMediaKind.Image => "image",
        WhatsappMediaKind.Video => "video",
        WhatsappMediaKind.Audio => "audio",
        _ => "document",
    };

    /// <summary>Maps a WhatsApp message "type" string to a media kind, if it is a media type.</summary>
    public static WhatsappMediaKind? ClassifyByType(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "image" => WhatsappMediaKind.Image,
        "sticker" => WhatsappMediaKind.Image,
        "video" => WhatsappMediaKind.Video,
        "audio" => WhatsappMediaKind.Audio,
        "voice" => WhatsappMediaKind.Audio,
        "document" => WhatsappMediaKind.Document,
        _ => null,
    };

    /// <summary>Classifies a MIME type against the configured allow-lists.</summary>
    public static WhatsappMediaKind? ClassifyByMime(string? mimeType, WhatsappMediaOptions options)
    {
        var mime = NormalizeMime(mimeType);
        if (string.IsNullOrEmpty(mime))
            return null;

        if (Contains(options.AllowedImageMimeTypes, mime)) return WhatsappMediaKind.Image;
        if (Contains(options.AllowedVideoMimeTypes, mime)) return WhatsappMediaKind.Video;
        if (Contains(options.AllowedAudioMimeTypes, mime)) return WhatsappMediaKind.Audio;
        if (Contains(options.AllowedDocumentMimeTypes, mime)) return WhatsappMediaKind.Document;

        // SVG can carry script and is never accepted, even under the lenient image/* fallback.
        var baseMime = mime.Split(';', 2)[0].Trim();
        if (baseMime == "image/svg+xml") return null;

        // Fall back to the top-level MIME group for inbound classification leniency.
        if (mime.StartsWith("image/", StringComparison.Ordinal)) return WhatsappMediaKind.Image;
        if (mime.StartsWith("video/", StringComparison.Ordinal)) return WhatsappMediaKind.Video;
        if (mime.StartsWith("audio/", StringComparison.Ordinal)) return WhatsappMediaKind.Audio;
        return null;
    }

    public static long MaxBytesFor(WhatsappMediaKind kind, WhatsappMediaOptions options) => kind switch
    {
        WhatsappMediaKind.Image => options.MaxImageBytes,
        WhatsappMediaKind.Video => options.MaxVideoBytes,
        WhatsappMediaKind.Audio => options.MaxAudioBytes,
        _ => options.MaxDocumentBytes,
    };

    /// <summary>
    /// Validates an outbound upload: the MIME type must be in an allow-list and the size
    /// within the configured per-kind maximum.
    /// </summary>
    public static MediaValidationResult Validate(string? contentType, long size, WhatsappMediaOptions options)
    {
        var kind = ClassifyByMime(contentType, options);
        if (kind is null)
            return MediaValidationResult.Invalid(
                $"Unsupported file type '{NormalizeMime(contentType) ?? "unknown"}'. Allowed: images (JPEG/PNG/WebP), video (MP4/3GP), audio (AAC/MP3/OGG/AMR) and documents (PDF/Office/text).");

        if (size <= 0)
            return MediaValidationResult.Invalid("The selected file is empty.");

        var max = MaxBytesFor(kind.Value, options);
        if (size > max)
            return MediaValidationResult.Invalid($"File is too large. Maximum size for {ToTypeString(kind.Value)} is {FormatBytes(max)}.");

        return MediaValidationResult.Valid(kind.Value);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024) return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes} B";
    }

    private static string? NormalizeMime(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return null;
        var value = mimeType.Trim().ToLowerInvariant();
        return value;
    }

    private static bool Contains(IEnumerable<string> list, string mime)
    {
        // Tolerate parameters (e.g. "audio/ogg; codecs=opus" vs "audio/ogg") by comparing base MIME.
        var baseMime = mime.Split(';', 2)[0].Trim();
        foreach (var item in list)
        {
            var normalized = item.Trim().ToLowerInvariant();
            if (normalized == mime || normalized.Split(';', 2)[0].Trim() == baseMime)
                return true;
        }
        return false;
    }
}

public sealed record MediaValidationResult(bool IsValid, WhatsappMediaKind Kind, string? Error)
{
    public static MediaValidationResult Valid(WhatsappMediaKind kind) => new(true, kind, null);
    public static MediaValidationResult Invalid(string error) => new(false, WhatsappMediaKind.Document, error);
}

namespace CureFlow.Application.Options;

/// <summary>
/// Configuration for WhatsApp conversation media (attachments sent/received in the inbox).
/// Limits default to WhatsApp Cloud API maximums.
/// </summary>
public class WhatsappMediaOptions
{
    public const string SectionName = "WhatsAppMedia";

    /// <summary>Relative directory under the content root where conversation media is stored.</summary>
    public string StoragePath { get; set; } = "whatsapp-media";

    /// <summary>Max size for image uploads (default 16 MB, the WhatsApp image limit).</summary>
    public long MaxImageBytes { get; set; } = 16L * 1024 * 1024;

    /// <summary>Max size for video uploads (default 16 MB, the WhatsApp video limit).</summary>
    public long MaxVideoBytes { get; set; } = 16L * 1024 * 1024;

    /// <summary>Max size for audio uploads (default 16 MB, the WhatsApp audio limit).</summary>
    public long MaxAudioBytes { get; set; } = 16L * 1024 * 1024;

    /// <summary>Max size for document uploads (default 100 MB, the WhatsApp document limit).</summary>
    public long MaxDocumentBytes { get; set; } = 100L * 1024 * 1024;

    public List<string> AllowedImageMimeTypes { get; set; } = new()
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    public List<string> AllowedVideoMimeTypes { get; set; } = new()
    {
        "video/mp4",
        "video/3gpp",
    };

    public List<string> AllowedAudioMimeTypes { get; set; } = new()
    {
        "audio/aac",
        "audio/mp4",
        "audio/mpeg",
        "audio/amr",
        "audio/ogg",
        "audio/ogg; codecs=opus",
    };

    public List<string> AllowedDocumentMimeTypes { get; set; } = new()
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain",
        "text/csv",
    };
}

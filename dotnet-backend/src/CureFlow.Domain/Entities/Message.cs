using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace CureFlow.Domain.Entities;

public class Message : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Guid? PatientId { get; set; }
    public string? WaMessageId { get; set; }
    public MessageDirection Direction { get; set; }
    public string Type { get; set; } = "text";
    public string Body { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MimeType { get; set; }
    public string? Caption { get; set; }

    /// <summary>Original file name of the attached media (when known).</summary>
    public string? FileName { get; set; }

    /// <summary>Size of the stored media file in bytes (when known).</summary>
    public long? MediaSize { get; set; }

    /// <summary>
    /// Relative path (under the content root) of the media file persisted in CureFlow storage.
    /// Used by the authenticated media download endpoint. Null for media hosted externally.
    /// </summary>
    public string? MediaStoredPath { get; set; }

    /// <summary>Optional thumbnail/preview URL for the media.</summary>
    public string? ThumbnailUrl { get; set; }

    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    public Guid? SenderUserId { get; set; }
}

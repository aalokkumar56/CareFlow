using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

public class WhatsAppMessage : BaseEntity
{
    public string Phone { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty; // text, template, media
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // sent, delivered, read, failed
    public string? ExternalMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

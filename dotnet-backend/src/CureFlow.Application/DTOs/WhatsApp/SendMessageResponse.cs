using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class SendMessageResponse
{
    public bool Success { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Error { get; set; }
}

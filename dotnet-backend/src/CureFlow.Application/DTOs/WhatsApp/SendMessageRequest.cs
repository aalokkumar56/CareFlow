using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class SendMessageRequest
{
    public string Phone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

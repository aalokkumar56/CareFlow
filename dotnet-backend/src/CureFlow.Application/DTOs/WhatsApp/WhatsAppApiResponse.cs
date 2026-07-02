using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class WhatsAppApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

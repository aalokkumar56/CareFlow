using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class SendMediaRequest
{
    public string Phone { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty; // image, video, document, audio
    public string MediaUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

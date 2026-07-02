using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class ComponentParameter
{
    public string Type { get; set; } = string.Empty; // text, video, image, etc.
    public string? Text { get; set; }
    public MediaLink? Video { get; set; }
    public MediaLink? Image { get; set; }
    public MediaLink? Document { get; set; }
    public MediaLink? Audio { get; set; }
}

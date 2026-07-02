using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class TemplateComponent
{
    public string Type { get; set; } = string.Empty; // header, body, footer
    public List<ComponentParameter> Parameters { get; set; } = new();
}

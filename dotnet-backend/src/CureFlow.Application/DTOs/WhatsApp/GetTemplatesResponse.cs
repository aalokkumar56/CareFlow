using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class GetTemplatesResponse
{
    public string Status { get; set; } = string.Empty;
    public List<TemplateDTO> Templates { get; set; } = new();
}

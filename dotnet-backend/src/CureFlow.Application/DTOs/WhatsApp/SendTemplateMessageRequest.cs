using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class SendTemplateMessageRequest
{
    public string Phone { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateLanguage { get; set; } = string.Empty;
    public List<TemplateComponent> Components { get; set; } = new();
}

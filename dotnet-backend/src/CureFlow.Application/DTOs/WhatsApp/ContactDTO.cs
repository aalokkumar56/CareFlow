using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class ContactDTO
{
    public string Id { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GroupId { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

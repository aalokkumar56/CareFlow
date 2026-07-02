using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class MakeContactRequest
{
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? GroupId { get; set; }
    public Dictionary<string, string>? CustomFields { get; set; }
}

// Response DTOs

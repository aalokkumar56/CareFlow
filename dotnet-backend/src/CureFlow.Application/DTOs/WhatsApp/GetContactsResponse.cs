using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class GetContactsResponse
{
    public string Status { get; set; } = string.Empty;
    public List<ContactDTO> Contacts { get; set; } = new();
}

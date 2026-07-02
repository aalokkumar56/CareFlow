using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class GetGroupsResponse
{
    public string Status { get; set; } = string.Empty;
    public List<GroupDTO> Groups { get; set; } = new();
}

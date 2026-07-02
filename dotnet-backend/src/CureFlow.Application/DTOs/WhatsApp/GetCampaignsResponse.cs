using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class GetCampaignsResponse
{
    public string Status { get; set; } = string.Empty;
    public List<CampaignDTO> Campaigns { get; set; } = new();
}

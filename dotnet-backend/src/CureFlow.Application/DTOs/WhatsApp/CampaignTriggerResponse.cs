using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class CampaignTriggerResponse
{
    public bool Success { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
}

namespace CureFlow.Application.DTOs;

public record UpdateCampaignRequest(
    string? Name = null,
    string? Description = null,
    string? MessageBody = null,
    DateTime? ScheduledAt = null,
    bool ClearSchedule = false,
    string? Status = null);

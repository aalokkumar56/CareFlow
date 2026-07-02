namespace CureFlow.Application.DTOs;

public record SuggestedCampaignDraftDto(
    Guid EventId,
    string Name,
    DateOnly EventDate,
    string Category,
    string Region,
    string Source,
    string SuggestedMessage,
    string DraftName);

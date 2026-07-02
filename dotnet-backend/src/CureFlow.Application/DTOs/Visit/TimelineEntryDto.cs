using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record TimelineEntryDto(
    string Type, DateTime OccurredAt, Guid? EntityId, string Title, string? Summary, object? Data);

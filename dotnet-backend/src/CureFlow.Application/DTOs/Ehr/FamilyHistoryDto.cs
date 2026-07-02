using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record FamilyHistoryDto(
    Guid Id, Guid PatientId, string Relation, string Condition,
    int? AgeOfOnset, string? Notes, DateTime CreatedAt);

// ===== Doctor's holistic view =====

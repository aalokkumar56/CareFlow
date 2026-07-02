using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record CreateFamilyHistoryRequest(
    Guid PatientId, string Relation, string Condition, int? AgeOfOnset, string? Notes);

// ===== Read DTOs =====

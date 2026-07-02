using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record AllergyDto(Guid Id, Guid PatientId, AllergyType Type, string Allergen,
    AllergySeverity Severity, string? Reaction, DateTime? FirstObserved, string? Notes,
    string? RecordedByName, DateTime CreatedAt);

// ===== EHR â€” Prescription =====

using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record CreateAllergyRequest(Guid PatientId, AllergyType Type, string Allergen,
    AllergySeverity Severity, string? Reaction, DateTime? FirstObserved, string? Notes);

using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record InjectionDto(
    Guid Id, string Name, string? GenericName, string? Strength, string? Site,
    PrescriptionRouteType Route, DateTime AdministeredAt, string AdministeredBy,
    Guid? AdministeredByUserId,
    string? BatchNumber, DateTime? ExpiryDate,
    string ReasonForInjection, string? AdverseReaction, string? Notes);

// ===== EHR â€” Vitals, Lab, Notes =====

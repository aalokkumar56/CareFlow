using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record InjectionRequest(
    string Name, string? GenericName, string? Strength, string? Site,
    PrescriptionRouteType Route, DateTime AdministeredAt, string AdministeredBy,
    Guid? AdministeredByUserId,
    string? BatchNumber, DateTime? ExpiryDate,
    string ReasonForInjection, string? AdverseReaction, string? Notes);

using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record PrescriptionItemDto(
    Guid Id, string DrugName, string? GenericName, string? Strength, string? Form,
    PrescriptionRouteType Route, string? Dosage, string? Frequency, string? Duration,
    string? Timing, int? Quantity,
    string ReasonForPrescribing,
    string? PossibleSideEffects, string? PatientInstructions,
    bool IsContinuation, bool IsAcute);

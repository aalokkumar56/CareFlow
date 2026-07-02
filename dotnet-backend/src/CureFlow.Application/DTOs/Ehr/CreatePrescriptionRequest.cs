using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record CreatePrescriptionRequest(
    Guid PatientId, Guid? AppointmentId, Guid? VisitId,
    string? Diagnosis, string? ChiefComplaint, string? ClinicalNotes,
    string? FollowUpAdvice, DateTime? NextVisitDate,
    List<PrescriptionItemRequest> Items,
    List<InjectionRequest>? Injections);

using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record PrescriptionDto(
    Guid Id, Guid PatientId, Guid? VisitId, Guid DoctorUserId, string DoctorName,
    DateTime PrescribedAt, string? Diagnosis, string? ChiefComplaint,
    string? ClinicalNotes, string? FollowUpAdvice, DateTime? NextVisitDate,
    IReadOnlyList<PrescriptionItemDto> Items,
    IReadOnlyList<InjectionDto> Injections);

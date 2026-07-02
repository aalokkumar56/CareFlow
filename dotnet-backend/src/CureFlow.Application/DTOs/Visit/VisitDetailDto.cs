using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record VisitDetailDto(
    Guid Id, Guid PatientId, Guid? AppointmentId, Guid DoctorUserId, string DoctorName,
    string? Department, DateTime VisitDate, DateTime? EndedAt, VisitStatus Status, string VisitType,
    string? Symptoms, string? Diagnosis, string? DoctorNotes,
    string? FollowUpAdvice, DateTime? FollowUpDate, string AttachmentsJson,
    IReadOnlyList<VitalSignsDto> Vitals,
    IReadOnlyList<ClinicalNoteDto> Notes,
    IReadOnlyList<PrescriptionDto> Prescriptions);

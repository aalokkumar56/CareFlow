using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record CreateVisitRequest(
    Guid PatientId, Guid? AppointmentId, Guid? DoctorUserId,
    string? Department, string? VisitType,
    string? Symptoms, string? Diagnosis, string? DoctorNotes,
    string? FollowUpAdvice, DateTime? FollowUpDate);

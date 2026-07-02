using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record VisitSummaryDto(
    Guid Id, Guid PatientId, Guid DoctorUserId, string DoctorName,
    string? Department, DateTime VisitDate, VisitStatus Status, string VisitType,
    string? Symptoms, string? Diagnosis, DateTime? FollowUpDate);

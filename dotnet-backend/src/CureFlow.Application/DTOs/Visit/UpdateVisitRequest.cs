using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record UpdateVisitRequest(
    string? Symptoms, string? Diagnosis, string? DoctorNotes,
    string? FollowUpAdvice, DateTime? FollowUpDate, VisitStatus? Status);

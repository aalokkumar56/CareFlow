using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record CreateClinicalNoteRequest(
    Guid PatientId, Guid? AppointmentId, Guid? VisitId,
    string? NoteType, string? Subjective, string? Objective, string? Assessment, string? Plan);

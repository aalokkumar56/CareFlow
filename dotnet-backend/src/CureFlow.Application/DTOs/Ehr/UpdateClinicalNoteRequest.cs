using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record UpdateClinicalNoteRequest(
    string? NoteType, string? Subjective, string? Objective, string? Assessment, string? Plan,
    Guid? PatientId = null, Guid? VisitId = null, Guid? AppointmentId = null,
    int? Revision = null, string? OriginalLanguage = null, bool Finalize = false);

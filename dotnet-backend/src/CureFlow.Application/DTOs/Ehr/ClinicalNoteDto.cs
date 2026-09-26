using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record ClinicalNoteDto(
    Guid Id, Guid PatientId, Guid? VisitId, string? AuthorName, DateTime CreatedAt,
    string NoteType, string Subjective, string Objective, string Assessment, string Plan,
    Guid? AppointmentId = null, Guid? AuthorUserId = null, string OriginalLanguage = "en-IN",
    string Status = "final_locked", DateTime? FinalizedAt = null, DateTime? EditableUntil = null,
    DateTime? UpdatedAt = null, DateTime? LastAutoSavedAt = null, int Revision = 1,
    bool CanEdit = false, DateTime? ServerTime = null);

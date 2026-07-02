using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record ClinicalNoteDto(
    Guid Id, Guid PatientId, Guid? VisitId, string? AuthorName, DateTime CreatedAt,
    string NoteType, string Subjective, string Objective, string Assessment, string Plan);

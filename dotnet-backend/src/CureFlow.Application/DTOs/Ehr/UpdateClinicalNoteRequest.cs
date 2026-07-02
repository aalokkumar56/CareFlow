using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record UpdateClinicalNoteRequest(
    string? NoteType, string? Subjective, string? Objective, string? Assessment, string? Plan);

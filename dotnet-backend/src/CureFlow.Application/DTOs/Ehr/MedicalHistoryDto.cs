using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record MedicalHistoryDto(
    Guid Id, Guid PatientId, string Category, string Title,
    DateTime? OnsetDate, bool IsOngoing, string? Description, DateTime CreatedAt);

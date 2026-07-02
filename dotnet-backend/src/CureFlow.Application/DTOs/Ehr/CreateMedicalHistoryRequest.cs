using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record CreateMedicalHistoryRequest(
    Guid PatientId, string Category, string Title, DateTime? OnsetDate, bool IsOngoing, string? Description);

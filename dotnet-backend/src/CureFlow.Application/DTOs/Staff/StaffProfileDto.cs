using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record StaffProfileDto(
    Guid Id, Guid UserId, string Name, string Email, UserRole Role,
    string? Phone, string? Department, string? Specialization, string? Qualification,
    decimal? ConsultationFee, StaffEmploymentType EmploymentType,
    string? Shift, string? WardAssignment, bool IsAvailable,
    DateTime CreatedAt);

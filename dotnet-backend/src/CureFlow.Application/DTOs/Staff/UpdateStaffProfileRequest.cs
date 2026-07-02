using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record UpdateStaffProfileRequest(
    string? Department, string? Specialization, string? Qualification,
    decimal? ConsultationFee, StaffEmploymentType? EmploymentType,
    string? Shift, string? WardAssignment, bool? IsAvailable);

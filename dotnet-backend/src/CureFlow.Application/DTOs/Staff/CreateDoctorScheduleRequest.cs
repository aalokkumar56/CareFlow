using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record CreateDoctorScheduleRequest(
    Guid StaffProfileId, int? DayOfWeek, DateTime? SpecificDate,
    string StartTime, string EndTime, bool IsAvailable = true, string? Notes = null);

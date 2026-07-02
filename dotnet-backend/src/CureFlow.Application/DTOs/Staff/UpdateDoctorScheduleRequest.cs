using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record UpdateDoctorScheduleRequest(
    int? DayOfWeek, DateTime? SpecificDate,
    TimeSpan? StartTime, TimeSpan? EndTime, bool? IsAvailable, string? Notes);

using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record DoctorScheduleDto(
    Guid Id, Guid StaffProfileId, int? DayOfWeek, DateTime? SpecificDate,
    TimeSpan StartTime, TimeSpan EndTime, bool IsAvailable, string? Notes);

using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record UpdateAppointmentRequest(
    DateTime? ScheduledAt,
    Guid? DoctorUserId,
    string? DoctorName,
    string? Department,
    string? ChiefComplaint,
    string? Notes,
    int? DurationMinutes);

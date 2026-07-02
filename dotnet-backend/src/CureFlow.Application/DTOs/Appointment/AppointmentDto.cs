using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record AppointmentDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string PatientPhone,
    Guid? DoctorUserId,
    string DoctorName,
    string Department,
    DateTime ScheduledAt,
    int DurationMinutes,
    string? ChiefComplaint,
    string? Notes,
    AppointmentStatus Status,
    decimal? ConsultationFee,
    bool IsPaid,
    DateTime CreatedAt,
    DateTime UpdatedAt);

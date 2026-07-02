using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record CreateAppointmentRequest(
    Guid PatientId, Guid? DoctorUserId, string? DoctorName, string Department,
    DateTime ScheduledAt, string? Notes);

using CureFlow.Domain.Enums;
namespace CureFlow.Application.DTOs;

public record SaveLeadRequest(string Phone, string? Name, int? Age, Gender Gender = Gender.Unknown);
public record BookLeadAppointmentRequest(string Name, Guid DoctorUserId, string Department,
    DateTime ScheduledAt, string? Notes, Guid? PatientId = null);
public record LeadImportResult(int Inserted, int Skipped, IReadOnlyList<string> Errors);

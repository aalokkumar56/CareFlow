using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record BookableDoctorDto(
    Guid UserId,
    Guid StaffProfileId,
    string Name,
    string? Department,
    string? Specialization,
    decimal? ConsultationFee,
    StaffEmploymentType EmploymentType);

public record AppointmentBookingOptionsDto(
    IReadOnlyList<BookableDoctorDto> Doctors,
    IReadOnlyList<string> Departments);

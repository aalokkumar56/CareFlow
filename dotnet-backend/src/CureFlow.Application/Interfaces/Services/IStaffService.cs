using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface IStaffService
{
    Task<Guid> CreateAsync(CreateStaffProfileRequest req, CancellationToken ct = default);
    Task<StaffProfileDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StaffProfileDto>> ListAsync(string? q, UserRole? role, CancellationToken ct = default);
    Task<IReadOnlyList<StaffProfileDto>> ListDoctorsAsync(string? q, CancellationToken ct = default);
    Task<IReadOnlyList<StaffProfileDto>> ListNursesAsync(string? q, CancellationToken ct = default);
    Task<AppointmentBookingOptionsDto> GetBookingOptionsAsync(CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateStaffProfileRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<Guid> AddScheduleAsync(CreateDoctorScheduleRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorScheduleDto>> ListSchedulesAsync(Guid staffProfileId, CancellationToken ct = default);
    Task UpdateScheduleAsync(Guid scheduleId, UpdateDoctorScheduleRequest req, CancellationToken ct = default);
    Task DeleteScheduleAsync(Guid scheduleId, CancellationToken ct = default);
}

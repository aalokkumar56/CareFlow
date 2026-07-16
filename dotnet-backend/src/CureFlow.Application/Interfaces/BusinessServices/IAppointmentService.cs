using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IAppointmentService
{
    Task<Guid> CreateAsync(Guid patientId, Guid? doctorUserId, string? doctorName, string department, DateTime scheduledAt, string? notes, CancellationToken ct = default);
    Task<AppointmentDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AppointmentDto>> ListAsync(string? status, DateTime? from, DateTime? to, Guid? doctorUserId, int page, int pageSize, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateAppointmentRequest req, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default);
}

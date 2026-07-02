using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface IPatientService
{
    Task<Guid> CreateAsync(CreatePatientRequest req, CancellationToken ct = default);
    Task<PatientDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PatientHolisticViewDto> GetHolisticViewAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<PatientSummaryDto>> ListAsync(string? q, string? status, string? department, string? tag, string? inquirySource, int page, int pageSize, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdatePatientRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<(int inserted, int skipped)> ImportCsvAsync(Stream csv, CancellationToken ct = default);
}

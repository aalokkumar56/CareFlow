using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

/// <summary>Result returned after an Excel/CSV import operation.</summary>
public record ImportResult(
    int Inserted,
    int Skipped,
    int BlankRows,
    List<ImportSkipRecord> SkipLog);

/// <summary>Reason a single row was skipped during import.</summary>
public record ImportSkipRecord(int RowNumber, string? Name, string? PhoneRaw, string Reason);

public interface IPatientService
{
    Task<Guid> CreateAsync(CreatePatientRequest req, CancellationToken ct = default);
    Task<PatientDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PatientHolisticViewDto> GetHolisticViewAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<PatientSummaryDto>> ListAsync(string? q, string? status, string? department, string? tag, string? inquirySource, int page, int pageSize, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdatePatientRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ImportResult> ImportCsvAsync(Stream csv, CancellationToken ct = default);
    Task<ImportResult> ImportExcelAsync(Stream stream, string fileExtension, CancellationToken ct = default);
}

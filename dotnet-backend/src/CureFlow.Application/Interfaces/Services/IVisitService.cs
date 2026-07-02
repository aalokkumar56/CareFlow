using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

public interface IVisitService
{
    Task<Guid> CreateAsync(CreateVisitRequest req, CancellationToken ct = default);
    Task<VisitDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VisitSummaryDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateVisitRequest req, CancellationToken ct = default);
    Task CompleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TimelineEntryDto>> GetTimelineAsync(Guid patientId, CancellationToken ct = default);
    Task<string> GetPrescriptionPrintHtmlAsync(Guid prescriptionId, CancellationToken ct = default);
    Task<LabReportUploadResultDto> UploadLabReportAsync(
        Guid patientId, Guid? visitId, string testName, Stream file, string fileName, string? contentType, long fileSize,
        CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadLabReportAsync(Guid id, CancellationToken ct = default);
}

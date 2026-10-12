namespace CureFlow.Application.Interfaces;
public record PatientExportInput(IReadOnlyList<Guid> PatientIds,IReadOnlyList<string> Fields,string Purpose);
public record PatientExportStatus(Guid Id,string State,long Revision,DateTime ExpiresAt);
public record PatientExportDownload(Stream Content,string FileName);
public interface IPatientExportService
{
    Task<IdempotentCommandResult> CreateAsync(PatientExportInput input,string key,CancellationToken ct);
    Task<PatientExportStatus> GetAsync(Guid id,CancellationToken ct);
    Task<PatientExportDownload> DownloadAsync(Guid id,CancellationToken ct);
}
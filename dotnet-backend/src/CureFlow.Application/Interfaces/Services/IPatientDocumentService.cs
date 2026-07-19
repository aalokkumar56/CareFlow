using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IPatientDocumentService
{
    Task<IReadOnlyList<PatientDocumentDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<PatientDocumentUploadResultDto> UploadAsync(
        Guid patientId,
        Guid? visitId,
        string? title,
        string documentType,
        Stream file,
        string fileName,
        string? contentType,
        long fileSize,
        CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

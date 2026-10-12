using CureFlow.Application.DTOs;
using CureFlow.Domain.Enums;
namespace CureFlow.Application.Interfaces;
public record PatientImportMapping(Guid FacilityId,int HeaderRow,IReadOnlyDictionary<string,int> Columns,string DateFormat,string CsvSeparator="comma",int Worksheet=0);
public record PatientImportDraft(string Name,string Phone,string? Email,int? Age,Gender Gender,BirthDateEvidence? BirthDate);
public record PatientImportCandidate(Guid PatientId,string Name,string Phone,string PatientTag);
public record PatientImportPreviewRow(int RowNumber,IReadOnlyList<string> RawValues,PatientImportDraft? Draft,IReadOnlyList<string> Errors,
    bool RepeatedFileRow,IReadOnlyList<PatientImportCandidate> Candidates,string ReviewTag,string Decision,Guid? PatientId);
public record PatientImportPreview(Guid BatchId,Guid FacilityId,string FileSha256,string Format,int RowCount,DateTime ExpiresAt,IReadOnlyList<PatientImportPreviewRow> Rows,int? NextAfterRow,string? ParseErrorCode=null);
public record PatientImportRowDecision(string Decision,string ExpectedReviewTag,bool AcknowledgePotentialDuplicates,string Reason);
public interface IPatientImportService
{
    Task<IdempotentCommandResult> StageAsync(Stream content,long length,string format,PatientImportMapping mapping,string key,CancellationToken ct);
    Task<PatientImportPreview> PreviewAsync(Guid batchId,int afterRow,int limit,CancellationToken ct);
    Task<IdempotentCommandResult> CommitRowAsync(Guid batchId,int rowNumber,PatientImportRowDecision decision,string key,CancellationToken ct);
}

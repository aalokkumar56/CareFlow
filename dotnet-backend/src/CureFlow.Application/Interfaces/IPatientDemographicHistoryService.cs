namespace CureFlow.Application.Interfaces;
public record PatientDemographicHistoryEntry(Guid Id,long Revision,DateTime RecordedAt,Guid? RecordedBy,string SourceType,string? SourceReference,
    string Reason,string VerificationStatus,string? BeforeValue,string AfterValue);
public interface IPatientDemographicHistoryService
{
    Task<IReadOnlyList<PatientDemographicHistoryEntry>> GetAsync(Guid patientId,long? beforeRevision,int limit,CancellationToken ct);
}

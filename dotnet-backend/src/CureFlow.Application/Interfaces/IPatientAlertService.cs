using CureFlow.Application.DTOs;
namespace CureFlow.Application.Interfaces;
public record PatientAlertInput(string Key,string Category,string Title,string? Detail,string Severity,bool Active,DemographicSourceInput Source,DateOnly? ReportedDeathDate,long? ExpectedRevision);
public record PatientAlertView(Guid AlertId,long Revision,string Key,string Category,string Title,string? Detail,string Severity,bool Active,
    string SourceType,string? SourceReference,string Reason,DateOnly? ReportedDeathDate,DateTime EffectiveFrom,Guid? RecordedBy);
public interface IPatientAlertService
{
    Task<IReadOnlyList<PatientAlertView>> ListAsync(Guid patientId,CancellationToken ct);
    Task<IReadOnlyList<PatientAlertView>> HistoryAsync(Guid patientId,Guid alertId,long? beforeRevision,CancellationToken ct);
    Task<IdempotentCommandResult> SaveAsync(Guid patientId,Guid? alertId,PatientAlertInput input,string key,CancellationToken ct);
}

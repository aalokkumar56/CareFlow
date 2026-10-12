namespace CureFlow.Application.Interfaces;
public record PatientContactInput(string ContactType,string Usage,string DisplayName,string? Value,string? Relationship,string? SourceReference,bool Preferred,bool IsActive,DateTime EffectiveFrom,DateTime? EffectiveUntil,long? ExpectedRevision);
public record PatientContactView(Guid ContactId,long Revision,string ContactType,string Usage,string DisplayName,string? Value,string? Relationship,string? SourceReference,string VerificationStatus,bool Preferred,bool IsActive,DateTime EffectiveFrom,DateTime? EffectiveUntil,bool IsEffectiveNow);
public interface IPatientContactService
{
    Task<IReadOnlyList<PatientContactView>> ListAsync(Guid patientId,int limit,CancellationToken ct);
    Task<IdempotentCommandResult> SaveAsync(Guid patientId,Guid? contactId,PatientContactInput input,string key,CancellationToken ct);
}
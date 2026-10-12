namespace CureFlow.Application.Interfaces;
public record PatientMergeInput(Guid DuplicatePatientId,Guid ReviewId,long ReviewRevision,string SourcePatientTag,string SurvivorPatientTag,string Reason);
public record PatientMappingReversalInput(Guid ReviewId,long ReviewRevision,long ExpectedMappingRevision,string SourcePatientTag,string SurvivorPatientTag,string Reason);
public record PatientResolution(Guid RequestedPatientId,Guid CanonicalPatientId,IReadOnlyList<Guid> MappingIds,bool RequiresPublicationReview);
public record PatientMappingJournalEntry(Guid Id,string Operation,DateTime AppliedAt,Guid? ActorId,string BeforeValue,string AfterValue,bool PublicationHold);
public record PatientMappingView(Guid Id,Guid SourcePatientId,Guid SurvivorPatientId,Guid ReviewVersionId,long Revision,string Reason,DateTime MergedAt,
    DateTime? ReversedAt,string? ReversalReason,IReadOnlyList<PatientMappingJournalEntry> Journal);
public interface IPatientMergeService
{
    Task<PatientMappingView> GetMappingAsync(Guid mappingId,CancellationToken ct);
    Task<IdempotentCommandResult> ReverseAsync(Guid mappingId,PatientMappingReversalInput input,string key,CancellationToken ct);
    Task<PatientResolution> ResolveAsync(Guid patientId,CancellationToken ct);
    Task<IdempotentCommandResult> MergeAsync(Guid survivorId,PatientMergeInput input,string key,CancellationToken ct);
}

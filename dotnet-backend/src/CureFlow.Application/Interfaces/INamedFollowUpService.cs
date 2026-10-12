namespace CureFlow.Application.Interfaces;
public record NamedFollowUpInput(Guid PatientId,string SourceType,Guid SourceId,long? ExpectedAppointmentRevision,
 Guid? AssignedUserId,Guid EscalationOwnerUserId,DateTime DueAtUtc,string Reason);
public record NamedFollowUpChange(long ExpectedRevision,Guid? AssignedUserId,Guid EscalationOwnerUserId,DateTime DueAtUtc,string Reason);
public record NamedFollowUpCompletion(long ExpectedRevision,string EvidenceReference,string Reason);
public record NamedFollowUpView(Guid Id,Guid PatientId,string SourceType,Guid SourceId,string SourceVersion,Guid? AssignedUserId,
 Guid EscalationOwnerUserId,DateTime DueAtUtc,string State,long Revision,DateTime? AcknowledgedAtUtc,DateTime? CompletedAtUtc,
 bool IsUnassigned,bool IsOverdue);
public record NamedFollowUpHistory(long Revision,string Action,string Reason,Guid? ActorUserId,DateTime RecordedAtUtc,
 Guid? AssignedUserId,Guid EscalationOwnerUserId,DateTime DueAtUtc,string State,Guid? EvidenceNoteId,int? EvidenceNoteRevision);
public interface INamedFollowUpService
{
 Task<CommandReceipt> CreateInTransactionAsync(ICureFlowDbSession store,NamedFollowUpInput input,CancellationToken ct);
 Task<IdempotentCommandResult> CreateAsync(NamedFollowUpInput input,string key,CancellationToken ct);
 Task<NamedFollowUpView> GetAsync(Guid id,CancellationToken ct);
 Task<IReadOnlyList<NamedFollowUpHistory>> HistoryAsync(Guid id,long? beforeRevision,CancellationToken ct);
 Task<IReadOnlyList<NamedFollowUpView>> EscalationsAsync(int limit,CancellationToken ct);
 Task<IdempotentCommandResult> AcknowledgeAsync(Guid id,long expectedRevision,string key,CancellationToken ct);
 Task<IdempotentCommandResult> ReassignAsync(Guid id,NamedFollowUpChange input,string key,CancellationToken ct);
 Task<IdempotentCommandResult> CompleteAsync(Guid id,NamedFollowUpCompletion input,string key,CancellationToken ct);
}
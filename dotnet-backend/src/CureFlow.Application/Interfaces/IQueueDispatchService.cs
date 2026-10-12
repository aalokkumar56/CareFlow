namespace CureFlow.Application.Interfaces;
public record QueuePriorityBand(int Rank,string Name);
public record QueueOrderingPolicy(string Version,int DefaultRank,IReadOnlyList<QueuePriorityBand> Bands);
public record QueueDispatchView(Guid QueueEntryId,Guid AppointmentId,Guid PatientId,long Token,int SortRank,
    long Revision,Guid? StartedBy,DateTime? StartedAtUtc);
public interface IQueueDispatchService
{
    Task<QueueOrderingPolicy> PolicyAsync(Guid facilityId,CancellationToken ct);
    Task<IReadOnlyList<QueueDispatchView>> WaitingAsync(Guid facilityId,DateOnly date,int take,CancellationToken ct);
    Task<QueueDispatchView> GetAsync(Guid facilityId,Guid queueEntryId,CancellationToken ct);
    Task<IReadOnlyList<QueuePriorityChangeView>> HistoryAsync(Guid facilityId,Guid queueEntryId,long afterRevision,int take,CancellationToken ct);
    Task<IdempotentCommandResult> OverrideAsync(Guid facilityId,Guid queueEntryId,long expectedRevision,int rank,string reason,string key,CancellationToken ct);
    Task<IdempotentCommandResult> StartAsync(Guid facilityId,Guid queueEntryId,long expectedRevision,string key,CancellationToken ct);
}
public record QueuePriorityChangeView(Guid Id,long Revision,int SortRank,string Reason,string PolicyVersion,Guid? Actor,DateTime RecordedAtUtc);

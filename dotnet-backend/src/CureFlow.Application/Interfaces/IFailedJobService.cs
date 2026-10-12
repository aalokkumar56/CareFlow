namespace CureFlow.Application.Interfaces;

public record FailedJobSummary(Guid Id, string EventType, int SchemaVersion, string State, int Attempts,
    int OperatorRetries, DateTime DueAt, string? FailureCode, long Revision);
public interface IFailedJobService
{
    Task<IReadOnlyList<FailedJobSummary>> ListAsync(int limit, CancellationToken ct);
    Task<FailedJobSummary> GetAsync(Guid id, CancellationToken ct);
    Task<IdempotentCommandResult> RetryAsync(Guid id, long expectedRevision, string key, CancellationToken ct);
}

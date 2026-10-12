namespace CureFlow.Application.Interfaces;

public enum CommandOutcome { Created, Updated, Deleted, Accepted, Completed }
// Minimal acknowledgement only: clinical bodies/file bytes are never cached.
public record CommandReceipt(Guid ResourceId, long? Revision, CommandOutcome Outcome);
public record IdempotentCommandResult(CommandReceipt Receipt, int StatusCode, bool Replayed);
public interface IIdempotentCommandExecutor
{
    Task<IdempotentCommandResult> ExecuteAsync<TIntent>(string commandNamespace, string key, TIntent intent,
        TimeSpan approvedRetryHorizon,
        Func<ICureFlowDbSession, CancellationToken, Task> authorizeCurrent,
        Func<ICureFlowDbSession, CancellationToken, Task<(CommandReceipt Receipt, int StatusCode)>> mutate,
        CancellationToken ct = default);
}

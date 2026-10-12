namespace CureFlow.Application.Interfaces;

public record DocumentScanRequest(Guid DocumentId, Guid Version, string Sha256, long Length, string ObjectKey);
public record DocumentScanResult(string Verdict, string Engine, string EngineVersion, string Code);
public interface IDocumentScanner
{
    Task<DocumentScanResult> ScanAsync(Stream content, CancellationToken ct);
}
// Preparation must be read-only/repeatable: no provider sends or local domain writes.
// Its result is committed only after rechecking immutable provenance and the live lease.
public interface IPreparedTransactionalOutboxConsumer : ITransactionalOutboxConsumer
{
    Task<object> PrepareAsync(OutboxEnvelope envelope, CancellationToken ct);
    Task HandlePreparedAsync(ICureFlowDbSession transaction, OutboxEnvelope envelope, object preparation, CancellationToken ct);
}

namespace CureFlow.Application.Interfaces;

public record OutboxEnvelope(Guid EventId, Guid TenantId, string EventType, int SchemaVersion,
    string AggregateType, Guid AggregateId, string AggregateVersion, string PayloadJson);
public interface IOutboxWriter
{
    Task WriteAsync<TPayload>(ICureFlowDbSession sourceTransaction, Guid eventId, string eventType, int schemaVersion,
        string aggregateType, Guid aggregateId, string aggregateVersion, TPayload payload, CancellationToken ct = default);
}
// Local DB effects only, using this transaction. Provider sends need a separate
// reviewed transport/idempotency/reconciliation adapter, never this clinical transaction.
public interface ITransactionalOutboxConsumer
{
    string EventType { get; }
    string ConsumerName { get; }
    bool SupportsSchema(int version);
    Task HandleAsync(ICureFlowDbSession transaction, OutboxEnvelope envelope, CancellationToken ct);
}

using System.Text;
using System.Text.Json;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
namespace CureFlow.Infrastructure.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    public async Task WriteAsync<TPayload>(ICureFlowDbSession sourceTransaction, Guid eventId, string eventType, int schemaVersion,
        string aggregateType, Guid aggregateId, string aggregateVersion, TPayload payload, CancellationToken ct = default)
    {
        if (!sourceTransaction.IsInTransaction || sourceTransaction.TenantId == Guid.Empty)
            throw new InvalidOperationException("Outbox writes require the source domain transaction and tenant scope.");
        if (eventId == Guid.Empty || aggregateId == Guid.Empty || schemaVersion < 1 || string.IsNullOrWhiteSpace(eventType)
            || eventType.Length > 96 || string.IsNullOrWhiteSpace(aggregateType) || aggregateType.Length > 64 || aggregateVersion.Length > 128)
            throw new InvalidOperationException("Outbox event identity/type/version must be explicit and bounded.");
        var body = JsonSerializer.Serialize(payload);
        if (Encoding.UTF8.GetByteCount(body) > 65536) throw new InvalidOperationException("Outbox payload exceeds its 64 KB limit.");
        var prior = await sourceTransaction.GetByIdAsync<OutboxEvent>(eventId, ct: ct);
        if (prior is not null)
        {
            if (prior.EventType != eventType || prior.SchemaVersion != schemaVersion || prior.AggregateType != aggregateType
                || prior.AggregateId != aggregateId || prior.AggregateVersion != aggregateVersion || prior.PayloadJson != body)
                throw new InvalidOperationException("An event identifier cannot be reused for different provenance.");
            return;
        }
        await sourceTransaction.InsertAsync(new OutboxEvent { Id = eventId, EventType = eventType, SchemaVersion = schemaVersion,
            AggregateType = aggregateType, AggregateId = aggregateId, AggregateVersion = aggregateVersion, PayloadJson = body }, ct: ct);
    }
}

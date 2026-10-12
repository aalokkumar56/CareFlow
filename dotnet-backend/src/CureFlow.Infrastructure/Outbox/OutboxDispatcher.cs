using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace CureFlow.Infrastructure.Outbox;

public sealed class OutboxDispatcher(ICureFlowDbSession db, ITenantContext tenant, IEnumerable<ITransactionalOutboxConsumer> consumers,
    IOptions<OutboxOptions> options, ILogger<OutboxDispatcher> logger)
{
    private readonly Guid _owner = Guid.NewGuid();
    public async Task RunBatchAsync(CancellationToken ct)
    {
        var settings = options.Value; settings.Validate();
        var handlers = consumers.ToDictionary(x => x.EventType, StringComparer.Ordinal);
        foreach (var handler in handlers.Values)
            if (handler.ConsumerName.Length is < 1 or > 48 || handler.ConsumerName.Any(char.IsControl))
                throw new InvalidOperationException("Outbox consumer identity must be bounded and server-defined.");
        IReadOnlyList<OutboxEvent> batch = [];
        await db.TransactionAsync(async store => batch = await store.QueryAsync<OutboxEvent>("""
            WITH candidates AS (
              SELECT "Id" FROM "OutboxEvents" WHERE "TenantId"=@TenantId AND NOT "IsDeleted"
                AND "State" IN ('pending','leased') AND "DueAt"<=CURRENT_TIMESTAMP
                AND ("LeaseUntil" IS NULL OR "LeaseUntil"<=CURRENT_TIMESTAMP)
              ORDER BY "DueAt","CreatedAt" LIMIT @size FOR UPDATE SKIP LOCKED)
            UPDATE "OutboxEvents" e SET "Revision"="Revision"+1,"State"='leased',"LeaseOwner"=@owner,"LeaseToken"=@token,
              "LeaseUntil"=CURRENT_TIMESTAMP+(@lease*INTERVAL '1 second'),"Attempts"="Attempts"+1,"UpdatedAt"=CURRENT_TIMESTAMP
            FROM candidates c WHERE e."Id"=c."Id" RETURNING e.*
            """, new { size = settings.BatchSize, owner = _owner, token = Guid.NewGuid(), lease = settings.LeaseSeconds }, ct: ct), ct);
        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();
            if (!await RenewAsync(item, settings.LeaseSeconds, ct)) continue;
            if (item.Attempts > settings.MaximumAttempts + item.OperatorRetries) { await ReleaseAsync(item, true, "AttemptsExhausted", ct); continue; }
            if (!handlers.TryGetValue(item.EventType, out var handler) || !handler.SupportsSchema(item.SchemaVersion))
            { await ReleaseAsync(item, true, "ConsumerOrSchemaUnavailable", ct); continue; }
            if (tenant is not CurrentTenant current) throw new InvalidOperationException("Outbox requires an established scoped service identity.");
            current.ServiceIdentity = "outbox:" + handler.ConsumerName;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(settings.HandlerTimeoutSeconds));
            try
            {
                var envelope = new OutboxEnvelope(item.Id, item.TenantId, item.EventType, item.SchemaVersion,
                    item.AggregateType, item.AggregateId, item.AggregateVersion, item.PayloadJson);
                var preparation = handler is IPreparedTransactionalOutboxConsumer prepared
                    ? await prepared.PrepareAsync(envelope, deadline.Token) : null;
                await db.TransactionAsync(async store =>
                {
                    var owned = await store.QueryFirstOrDefaultAsync<OutboxEvent>(
                        "SELECT * FROM \"OutboxEvents\" WHERE \"Id\"=@Id AND \"TenantId\"=@TenantId AND \"LeaseOwner\"=@owner AND \"LeaseToken\"=@token AND \"LeaseUntil\">CURRENT_TIMESTAMP AND \"State\"='leased' FOR UPDATE",
                        new { item.Id, owner = _owner, token = item.LeaseToken }, ct: deadline.Token);
                    if (owned is null) return;
                    var receipt = await store.QueryFirstOrDefaultAsync<Guid?>(
                        "SELECT \"Id\" FROM \"OutboxConsumerReceipts\" WHERE \"TenantId\"=@TenantId AND \"EventId\"=@Id AND \"ConsumerName\"=@name",
                        new { item.Id, name = handler.ConsumerName }, ct: deadline.Token);
                    if (!receipt.HasValue)
                    {
                        if (handler is IPreparedTransactionalOutboxConsumer preparedHandler)
                            await preparedHandler.HandlePreparedAsync(store, envelope, preparation!, deadline.Token);
                        else await handler.HandleAsync(store, new(item.Id, item.TenantId, item.EventType, item.SchemaVersion,
                            item.AggregateType, item.AggregateId, item.AggregateVersion, item.PayloadJson), deadline.Token);
                        await store.InsertAsync(new OutboxConsumerReceipt { EventId = item.Id, ConsumerName = handler.ConsumerName }, ct: deadline.Token);
                    }
                    var completed = await store.ExecuteAsync(
                        "UPDATE \"OutboxEvents\" SET \"Revision\"=\"Revision\"+1,\"State\"='completed',\"CompletedAt\"=CURRENT_TIMESTAMP,\"LeaseUntil\"=NULL,\"LeaseOwner\"=NULL,\"LeaseToken\"=NULL,\"FailureCode\"=NULL,\"UpdatedAt\"=CURRENT_TIMESTAMP WHERE \"Id\"=@Id AND \"LeaseOwner\"=@owner AND \"LeaseToken\"=@token AND \"LeaseUntil\">CURRENT_TIMESTAMP",
                        new { item.Id, owner = _owner, token = item.LeaseToken }, ct: deadline.Token);
                    if (completed != 1) throw new InvalidOperationException("Outbox lease expired before local effect commit.");
                }, deadline.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                await ReleaseAsync(item, item.Attempts >= settings.MaximumAttempts + item.OperatorRetries, "ConsumerFailed", ct);
                logger.LogWarning("Outbox local consumer failed; retained event {EventId}, attempt {Attempt}", item.Id, item.Attempts);
            }
        }
    }
    private async Task<bool> RenewAsync(OutboxEvent item, int seconds, CancellationToken ct) => await db.ExecuteAsync(
        "UPDATE \"OutboxEvents\" SET \"Revision\"=\"Revision\"+1,\"LeaseUntil\"=CURRENT_TIMESTAMP+(@seconds*INTERVAL '1 second') WHERE \"Id\"=@Id AND \"TenantId\"=@TenantId AND \"LeaseOwner\"=@owner AND \"LeaseToken\"=@token AND \"LeaseUntil\">CURRENT_TIMESTAMP AND \"State\"='leased'",
        new { item.Id, owner = _owner, token = item.LeaseToken, seconds }, ct: ct) == 1;
    private Task<int> ReleaseAsync(OutboxEvent item, bool failed, string code, CancellationToken ct) => db.ExecuteAsync(
        "UPDATE \"OutboxEvents\" SET \"Revision\"=\"Revision\"+1,\"State\"=@state,\"FailureCode\"=@code,\"DueAt\"=CURRENT_TIMESTAMP+(@seconds*INTERVAL '1 second'),\"LeaseOwner\"=NULL,\"LeaseToken\"=NULL,\"LeaseUntil\"=NULL,\"UpdatedAt\"=CURRENT_TIMESTAMP WHERE \"Id\"=@Id AND \"TenantId\"=@TenantId AND \"LeaseOwner\"=@owner AND \"LeaseToken\"=@token",
        new { item.Id, owner = _owner, token = item.LeaseToken, state = failed ? "failed" : "pending", code,
            seconds = Math.Min(3600, 5 * (1 << Math.Min(item.Attempts, 9))) + Random.Shared.Next(0, 10) }, ct: ct);
}



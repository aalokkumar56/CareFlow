using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Options;
namespace CureFlow.Infrastructure.Outbox;

public sealed class FailedJobService(ICureFlowDbSession db, ITenantContext tenant, IUserPermissionService permissions,
    IClinicalAccessAuditService audit, IIdempotentCommandExecutor commands,
    IEnumerable<ITransactionalOutboxConsumer> consumers, IOptions<OutboxOptions> options) : IFailedJobService
{
    private async Task RequireAsync(string capability, ICureFlowDbSession store, CancellationToken ct)
    {
        if (!tenant.IsAuthenticated || tenant.IsPlatformUser || !tenant.UserId.HasValue || tenant.TenantId == Guid.Empty)
            throw new DomainException("A current hospital operator is required.", 403);
        var user = await store.GetByIdAsync<User>(tenant.UserId.Value, ct: ct);
        if (user is null || !user.IsActive || !(await permissions.GetPermissionCodesAsync(user, ct, store)).Contains(capability, StringComparer.Ordinal))
            throw new DomainException("You do not have permission to manage this job.", 403);
    }
    public async Task<IReadOnlyList<FailedJobSummary>> ListAsync(int limit, CancellationToken ct)
    {
        await RequireAsync(CureFlowPermissions.JobView, db, ct);
        var result = await db.QueryAsync<FailedJobSummary>("""
            SELECT "Id","EventType","SchemaVersion","State","Attempts","OperatorRetries","DueAt","FailureCode","Revision"
            FROM "OutboxEvents" WHERE "TenantId"=@TenantId AND "State"='failed' AND NOT "IsDeleted"
            ORDER BY "DueAt","Id" LIMIT @limit
            """, new { limit = Math.Clamp(limit, 1, 100) }, ct: ct);
        await audit.RecordAsync(new(null, "OutboxEvent", null, "inspect_failed", "authorized", Purpose: "operations"), ct);
        return result;
    }
    public async Task<FailedJobSummary> GetAsync(Guid id, CancellationToken ct)
    {
        await RequireAsync(CureFlowPermissions.JobView, db, ct);
        var row = await db.GetByIdAsync<OutboxEvent>(id, ct: ct) ?? throw new NotFoundException("Job");
        await audit.RecordAsync(new(null, "OutboxEvent", row.Id, "inspect", "authorized", row.Revision.ToString(), "operations"), ct);
        return new(row.Id, row.EventType, row.SchemaVersion, row.State, row.Attempts, row.OperatorRetries, row.DueAt, row.FailureCode, row.Revision);
    }
    public Task<IdempotentCommandResult> RetryAsync(Guid id, long expectedRevision, string key, CancellationToken ct)
    {
        options.Value.Validate();
        if (expectedRevision < 1) throw new DomainException("Supply the current job version.", 428);
        return commands.ExecuteAsync("outbox.operator-retry", key, new { JobId = id, ExpectedRevision = expectedRevision },
            TimeSpan.FromHours(24), async (store, token) =>
            {
                await RequireAsync(CureFlowPermissions.JobRetry, store, token);
                if (await store.GetByIdAsync<OutboxEvent>(id, ct: token) is null) throw new NotFoundException("Job");
            }, async (store, token) =>
            {
                var row = await store.QueryFirstOrDefaultAsync<OutboxEvent>("""
                    SELECT * FROM "OutboxEvents" WHERE "Id"=@id AND "TenantId"=@TenantId AND NOT "IsDeleted" FOR UPDATE
                    """, new { id }, ct: token) ?? throw new NotFoundException("Job");
                if (row.Revision != expectedRevision) throw new DomainException("Job changed. Reload before retrying.", 412);
                if (row.State != "failed" || row.LeaseOwner.HasValue || row.LeaseToken.HasValue || row.LeaseUntil.HasValue)
                    throw new DomainException("Only a failed job without a lease can be retried.", 409);
                if (row.OperatorRetries >= options.Value.OperatorRetryLimit || row.Attempts >= options.Value.MaximumAttempts + options.Value.OperatorRetryLimit)
                    throw new DomainException("Job retry budget is exhausted; operator review is required.", 409);
                var matches = consumers.Where(x => x.EventType == row.EventType && x.SupportsSchema(row.SchemaVersion)).ToArray();
                if (matches.Length != 1) throw new DomainException("An approved consumer for this schema is unavailable.", 409);
                var changed = await store.ExecuteAsync("""
                    UPDATE "OutboxEvents" SET "State"='pending',"DueAt"=CURRENT_TIMESTAMP,"FailureCode"=NULL,
                      "OperatorRetries"="OperatorRetries"+1,"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP
                    WHERE "Id"=@id AND "TenantId"=@TenantId AND "Revision"=@expectedRevision AND "State"='failed'
                    """, new { id, expectedRevision }, ct: token);
                if (changed != 1) throw new DomainException("Job changed. Reload before retrying.", 412);
                await audit.RecordAsync(new(null, "OutboxEvent", id, "retry", "accepted", (row.Revision+1).ToString(), "operations"), token, store);
                return (new CommandReceipt(id, row.Revision + 1, CommandOutcome.Accepted), 202);
            }, ct);
    }
}

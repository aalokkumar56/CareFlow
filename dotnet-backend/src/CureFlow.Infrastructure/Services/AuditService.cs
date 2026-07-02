using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;

    public AuditService(ICureFlowDbSession db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task LogAsync(string action, string? entityType = null, string? entityId = null,
        object? metadata = null, CancellationToken ct = default)
    {
        if (_tenant.TenantId == Guid.Empty) return;
        var log = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserId = _tenant.UserId,
            UserName = _tenant.UserEmail,
            MetadataJson = metadata == null ? "{}" : System.Text.Json.JsonSerializer.Serialize(metadata),
        };
        await _db.InsertAsync(log, ct: ct);
    }

    public async Task<IReadOnlyList<object>> ListAsync(int limit, CancellationToken ct = default)
    {
        var take = Math.Clamp(limit, 1, 500);
        var where = SqlFragments.WhereActive<AuditLog>(ignoreTenant: false);
        var rows = await _db.QueryAsync<AuditLog>(
            $"""
            SELECT * FROM "AuditLogs"
            WHERE {where}
            ORDER BY "CreatedAt" DESC
            LIMIT @take
            """,
            new { take },
            ct: ct);
        return rows.Cast<object>().ToList();
    }
}

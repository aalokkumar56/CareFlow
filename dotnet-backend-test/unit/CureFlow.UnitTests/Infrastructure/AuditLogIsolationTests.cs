using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Services;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>Audit log tenant isolation at the service layer.</summary>
[Collection("DatabaseIntegration")]
public class AuditLogIsolationTests
{
    [Fact]
    public async Task TenantA_CannotRead_TenantB_AuditLogs()
    {
        var connectionString = TestDbConnection.Resolve();
        if (connectionString is null)
            return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        Guid alphaTenantId;
        Guid betaTenantId;

        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await PostgresRlsSession.ConfigureAsync(conn, Guid.Empty, platformBypass: true);
            alphaTenantId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
                new { slug = "care-cure-althan" });
            betaTenantId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
                new { slug = "city-hospital-surat" });
        }

        if (alphaTenantId == Guid.Empty || betaTenantId == Guid.Empty)
            return;

        var marker = $"e2e-audit-isolation-{Guid.NewGuid():N}";

        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await PostgresRlsSession.ConfigureAsync(conn, Guid.Empty, platformBypass: true);
            await conn.ExecuteAsync(
                """
                INSERT INTO "AuditLogs"
                  ("Id", "TenantId", "Action", "EntityType", "MetadataJson", "CreatedAt", "UpdatedAt", "IsActive", "IsDeleted")
                VALUES
                  (@id, @tenantId, @action, 'patient', '{}', NOW(), NOW(), true, false)
                """,
                new { id = Guid.NewGuid(), tenantId = alphaTenantId, action = marker });
        }

        var betaCtx = new CurrentTenant { TenantId = betaTenantId, IsAuthenticated = true, UserEmail = "beta@test" };
        var betaSession = new CureFlowDbSession(dataSource, betaCtx);
        var betaAudit = new AuditService(betaSession, betaCtx);

        var betaLogs = await betaAudit.ListAsync(500);
        betaLogs.Cast<AuditLog>().Should().NotContain(l => l.Action == marker);

        var alphaCtx = new CurrentTenant { TenantId = alphaTenantId, IsAuthenticated = true, UserEmail = "alpha@test" };
        var alphaSession = new CureFlowDbSession(dataSource, alphaCtx);
        var alphaAudit = new AuditService(alphaSession, alphaCtx);

        var alphaLogs = await alphaAudit.ListAsync(500);
        alphaLogs.Cast<AuditLog>().Should().Contain(l => l.Action == marker);
    }
}

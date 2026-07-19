using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Dapper;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>PostgreSQL RLS enforcement at the database layer.</summary>
public class TenantRlsIsolationTests
{
    [Fact]
    public async Task RawSql_WithoutTenantContext_ReturnsZeroRowsUnderRls()
    {
        var connectionString = TestDbConnection.Resolve();
        if (connectionString is null)
            return;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        if (await PostgresRlsSession.CurrentRoleBypassesRlsAsync(conn))
            return;

        var count = await conn.QueryFirstOrDefaultAsync<int>(
            """SELECT COUNT(*) FROM "Patients" WHERE "IsDeleted" = false""");

        count.Should().Be(0, "RLS must block unscoped reads when no session tenant is set");
    }

    [Fact]
    public async Task RawSql_WithTenantContext_ReturnsOnlyTenantRows()
    {
        var connectionString = TestDbConnection.Resolve();
        if (connectionString is null)
            return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        await using (var probe = await dataSource.OpenConnectionAsync())
        {
            if (await PostgresRlsSession.CurrentRoleBypassesRlsAsync(probe))
                return;
        }

        Guid alphaTenantId;
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await PostgresRlsSession.ConfigureAsync(conn, Guid.Empty, platformBypass: true);
            alphaTenantId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
                new { slug = "care-cure-althan" });
        }

        if (alphaTenantId == Guid.Empty)
            return;

        await using var scopedConn = await dataSource.OpenConnectionAsync();
        await PostgresRlsSession.ConfigureAsync(scopedConn, alphaTenantId, platformBypass: false);

        var ownCount = await scopedConn.QueryFirstOrDefaultAsync<int>(
            """
            SELECT COUNT(*) FROM "Patients"
            WHERE "IsDeleted" = false AND "Name" = @name
            """,
            new { name = "Althan Exclusive Patient" });
        ownCount.Should().BeGreaterThan(0, "tenant-scoped session should see own patients");

        var otherCount = await scopedConn.QueryFirstOrDefaultAsync<int>(
            """
            SELECT COUNT(*) FROM "Patients"
            WHERE "IsDeleted" = false AND "Name" = @name
            """,
            new { name = "Surat Exclusive Patient" });
        otherCount.Should().Be(0, "tenant-scoped session must not see other tenant patients");
    }

    [Fact]
    public async Task CrossTenant_GetById_ReturnsNull_UnderRls()
    {
        var connectionString = TestDbConnection.Resolve();
        if (connectionString is null)
            return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        Guid alphaTenantId;
        Guid betaTenantId;
        Guid alphaPatientId;

        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await PostgresRlsSession.ConfigureAsync(conn, Guid.Empty, platformBypass: true);
            alphaTenantId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
                new { slug = "care-cure-althan" });
            betaTenantId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
                new { slug = "city-hospital-surat" });
            alphaPatientId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """
                SELECT "Id" FROM "Patients"
                WHERE "TenantId" = @tenantId AND "Name" = @name AND "IsDeleted" = false
                LIMIT 1
                """,
                new { tenantId = alphaTenantId, name = "Althan Exclusive Patient" });
        }

        if (alphaTenantId == Guid.Empty || betaTenantId == Guid.Empty || alphaPatientId == Guid.Empty)
            return;

        var betaTenant = new CurrentTenant { TenantId = betaTenantId, IsAuthenticated = true };
        var betaSession = new CureFlowDbSession(dataSource, betaTenant);

        var leaked = await betaSession.GetByIdAsync<Patient>(alphaPatientId, ct: default);
        leaked.Should().BeNull("RLS + app filters must block cross-tenant patient reads");
    }
}

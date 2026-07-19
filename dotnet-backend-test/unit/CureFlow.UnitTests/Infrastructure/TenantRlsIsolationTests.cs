using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Dapper;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

/// <summary>PostgreSQL RLS enforcement at the database layer (non-bypass login role).</summary>
public class TenantRlsIsolationTests
{
    [Fact]
    public async Task RawSql_WithoutTenantContext_ReturnsZeroRowsUnderRls()
    {
        await TestSeedHelper.EnsureMultiHospitalAsync(TestDbConnection.Require());
        var connectionString = TestDbConnection.RequireRlsSubject();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        (await PostgresRlsSession.CurrentRoleBypassesRlsAsync(conn))
            .Should().BeFalse("cureflow_rls_test must be subject to RLS");

        var count = await conn.QueryFirstOrDefaultAsync<int>(
            """SELECT COUNT(*) FROM "Patients" WHERE "IsDeleted" = false""");

        count.Should().Be(0, "RLS must block unscoped reads when no session tenant is set");
    }

    [Fact]
    public async Task RawSql_WithTenantContext_ReturnsOnlyTenantRows()
    {
        await TestSeedHelper.EnsureMultiHospitalAsync(TestDbConnection.Require());
        var connectionString = TestDbConnection.RequireRlsSubject();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // set_config(..., is_local=true) only survives inside a transaction (matches CureFlowDbSession).
        await using var tx = await conn.BeginTransactionAsync();

        await PostgresRlsSession.ConfigureAsync(conn, Guid.Empty, platformBypass: true);
        var alphaTenantId = await conn.QueryFirstOrDefaultAsync<Guid>(
            """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
            new { slug = "care-cure-althan" });
        alphaTenantId.Should().NotBe(Guid.Empty, "multi-hospital seed must include care-cure-althan");

        await PostgresRlsSession.ConfigureAsync(conn, alphaTenantId, platformBypass: false);

        var ownCount = await conn.QueryFirstOrDefaultAsync<int>(
            """
            SELECT COUNT(*) FROM "Patients"
            WHERE "IsDeleted" = false AND "Name" = @name
            """,
            new { name = "Althan Exclusive Patient" });
        ownCount.Should().BeGreaterThan(0, "tenant-scoped session should see own patients");

        var otherCount = await conn.QueryFirstOrDefaultAsync<int>(
            """
            SELECT COUNT(*) FROM "Patients"
            WHERE "IsDeleted" = false AND "Name" = @name
            """,
            new { name = "Surat Exclusive Patient" });
        otherCount.Should().Be(0, "tenant-scoped session must not see other tenant patients");

        await tx.CommitAsync();
    }

    [Fact]
    public async Task CrossTenant_GetById_ReturnsNull_UnderRls()
    {
        await TestSeedHelper.EnsureMultiHospitalAsync(TestDbConnection.Require());
        var connectionString = TestDbConnection.RequireRlsSubject();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        Guid alphaTenantId;
        Guid betaTenantId;
        Guid alphaPatientId;

        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await using var tx = await conn.BeginTransactionAsync();
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
            await tx.CommitAsync();
        }

        alphaTenantId.Should().NotBe(Guid.Empty);
        betaTenantId.Should().NotBe(Guid.Empty);
        alphaPatientId.Should().NotBe(Guid.Empty);

        // CureFlowDbSession opens its own transaction around ConfigureAsync + query.
        var betaTenant = new CurrentTenant { TenantId = betaTenantId, IsAuthenticated = true };
        var betaSession = new CureFlowDbSession(dataSource, betaTenant);

        var leaked = await betaSession.GetByIdAsync<Patient>(alphaPatientId, ct: default);
        leaked.Should().BeNull("RLS + app filters must block cross-tenant patient reads");
    }
}

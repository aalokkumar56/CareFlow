using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Persistence.Seeders;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace CureFlow.Tests;

/// <summary>
/// Verifies tenant-scoped SQL filters and cross-tenant patient isolation at the DB layer.
/// Integration tests skip when PostgreSQL is unavailable (set CUREFLOW_TEST_CONNECTION).
/// </summary>
public class TenantIsolationTests
{
    [Fact]
    public void WhereActive_Patient_IncludesTenantFilter()
    {
        var where = SqlFragments.WhereActive<Patient>(false);
        where.Should().Contain("TenantId");
    }

    [Fact]
    public void WhereActive_Conversation_IncludesTenantFilter()
    {
        var where = SqlFragments.WhereActive<Conversation>(false);
        where.Should().Contain("TenantId");
    }

    [Fact]
    public void WhereActive_Appointment_IncludesTenantFilter()
    {
        var where = SqlFragments.WhereActive<Appointment>(false);
        where.Should().Contain("TenantId");
    }

    [Fact]
    public void WhereActive_IgnoreTenant_SkipsTenantFilter()
    {
        var where = SqlFragments.WhereActive<Patient>(ignoreTenant: true);
        where.Should().Be("1=1");
    }

    [Fact]
    public async Task CrossTenant_GetById_ReturnsNull_WhenPatientBelongsToOtherTenant()
    {
        var connectionString = ResolveConnectionString();
        if (connectionString is null)
        {
            return; // skip — no DB
        }

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
        {
            // Seeder not run yet — not a failure for unit-only CI
            return;
        }

        var betaTenant = new CurrentTenant { TenantId = betaTenantId, IsAuthenticated = true };
        var betaSession = new CureFlowDbSession(dataSource, betaTenant);

        var leaked = await betaSession.GetByIdAsync<Patient>(alphaPatientId, ct: default);
        leaked.Should().BeNull("Tenant B must not read Tenant A patient by ID");

        var alphaTenantCtx = new CurrentTenant { TenantId = alphaTenantId, IsAuthenticated = true };
        var alphaSession = new CureFlowDbSession(dataSource, alphaTenantCtx);
        var own = await alphaSession.GetByIdAsync<Patient>(alphaPatientId, ct: default);
        own.Should().NotBeNull();
        own!.Name.Should().Be("Althan Exclusive Patient");
    }

    private static string? ResolveConnectionString() => TestDbConnection.Resolve();
}

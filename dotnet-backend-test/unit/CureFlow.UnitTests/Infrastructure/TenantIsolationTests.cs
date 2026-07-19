using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Dapper;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

/// <summary>
/// Verifies tenant-scoped SQL filters and cross-tenant patient isolation at the DB layer.
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
        var connectionString = TestDbConnection.Require();
        await TestSeedHelper.EnsureMultiHospitalAsync(connectionString);

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

        alphaTenantId.Should().NotBe(Guid.Empty);
        betaTenantId.Should().NotBe(Guid.Empty);
        alphaPatientId.Should().NotBe(Guid.Empty);

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
}

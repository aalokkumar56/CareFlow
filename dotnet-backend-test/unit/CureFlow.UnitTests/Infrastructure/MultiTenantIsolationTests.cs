using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Persistence.Seeders;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

/// <summary>
/// Cross-tenant isolation and uniqueness constraints at the DB layer (P0 doc 01).
/// </summary>
public class MultiTenantIsolationTests
{
    [Fact]
    public void MultiHospitalE2eSeeder_DefinesThreeDistinctHospitals()
    {
        MultiHospitalE2eSeeder.Hospitals.Should().HaveCount(3);
        MultiHospitalE2eSeeder.Hospitals.Select(h => h.Slug).Should().OnlyHaveUniqueItems();
        MultiHospitalE2eSeeder.Hospitals.Select(h => h.AdminEmail).Should().OnlyHaveUniqueItems();
        MultiHospitalE2eSeeder.Hospitals.Select(h => h.ExclusivePatientName).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task TenantA_CannotGetTenantBPatient_ById()
    {
        var connectionString = TestDbConnection.Require();
        await TestSeedHelper.EnsureMultiHospitalAsync(connectionString);

        var (tenantAId, tenantBId, patientAId) = await ResolveSeedIdsAsync(connectionString);
        tenantAId.Should().NotBe(Guid.Empty);
        tenantBId.Should().NotBe(Guid.Empty);
        patientAId.Should().NotBe(Guid.Empty);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var tenantBSession = new CureFlowDbSession(dataSource, new CurrentTenant
        {
            TenantId = tenantBId,
            IsAuthenticated = true,
        });

        var leaked = await tenantBSession.GetByIdAsync<Patient>(patientAId, ct: default);
        leaked.Should().BeNull("Tenant B must not read Tenant A patient by ID");
    }

    [Fact]
    public async Task TenantA_CannotListTenantBPatients()
    {
        var connectionString = TestDbConnection.Require();
        await TestSeedHelper.EnsureMultiHospitalAsync(connectionString);

        var (tenantAId, tenantBId, patientAId) = await ResolveSeedIdsAsync(connectionString);
        tenantAId.Should().NotBe(Guid.Empty);
        tenantBId.Should().NotBe(Guid.Empty);
        patientAId.Should().NotBe(Guid.Empty);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var tenantBSession = new CureFlowDbSession(dataSource, new CurrentTenant
        {
            TenantId = tenantBId,
            IsAuthenticated = true,
        });

        var patients = await tenantBSession.GetAllAsync<Patient>(ct: default);
        patients.Should().NotContain(p => p.Id == patientAId);
        patients.Should().OnlyContain(p => p.TenantId == tenantBId);
        patients.Should().NotContain(p => p.Name == MultiHospitalE2eSeeder.Hospitals[0].ExclusivePatientName);
    }

    [Fact]
    public async Task ThirdTenantRegistration_DoesNotCollide_OnSlugOrEmail()
    {
        var connectionString = TestDbConnection.Require();
        await TestSeedHelper.EnsureMultiHospitalAsync(connectionString);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await dataSource.OpenConnectionAsync();

        var slugs = MultiHospitalE2eSeeder.Hospitals.Select(h => h.Slug).ToList();
        var emails = MultiHospitalE2eSeeder.Hospitals.Select(h => h.AdminEmail.ToLower()).ToList();

        var tenantCount = await conn.QuerySingleAsync<int>(
            """
            SELECT COUNT(*) FROM "Tenants"
            WHERE "Slug" = ANY(@slugs) AND "IsDeleted" = false
            """,
            new { slugs = slugs.ToArray() });

        tenantCount.Should().Be(3, "each E2E hospital slug must map to exactly one tenant");

        var systemSession = new CureFlowDbSession(dataSource, new CurrentTenant());
        var duplicateSlug = MultiHospitalE2eSeeder.Hospitals[0].Slug;

        var actSlug = () => systemSession.InsertAsync(new Tenant
        {
            Slug = duplicateSlug,
            Name = "Collision Test",
            ContactEmail = "collision-slug@test.local",
            Plan = SubscriptionPlan.Trial,
            SubscriptionStatus = SubscriptionStatus.Trialing,
        }, ignoreTenant: true);

        await actSlug.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);

        foreach (var hospital in MultiHospitalE2eSeeder.Hospitals)
        {
            var tenantId = await conn.QueryFirstOrDefaultAsync<Guid>(
                """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
                new { slug = hospital.Slug });
            tenantId.Should().NotBe(Guid.Empty);

            var actEmail = () => systemSession.InsertAsync(new User
            {
                TenantId = tenantId,
                Name = "Duplicate Email",
                Email = hospital.AdminEmail,
                Role = UserRole.TenantOwner,
                PasswordHash = "not-a-real-hash",
            }, ignoreTenant: true);

            await actEmail.Should().ThrowAsync<PostgresException>()
                .Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
        }

        emails.Should().OnlyHaveUniqueItems();
        slugs.Should().OnlyHaveUniqueItems();
    }

    private static async Task<(Guid TenantAId, Guid TenantBId, Guid PatientAId)> ResolveSeedIdsAsync(
        string connectionString)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await dataSource.OpenConnectionAsync();

        var hospitalA = MultiHospitalE2eSeeder.Hospitals[0];
        var hospitalB = MultiHospitalE2eSeeder.Hospitals[1];

        var tenantAId = await conn.QueryFirstOrDefaultAsync<Guid>(
            """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
            new { slug = hospitalA.Slug });
        var tenantBId = await conn.QueryFirstOrDefaultAsync<Guid>(
            """SELECT "Id" FROM "Tenants" WHERE "Slug" = @slug AND "IsDeleted" = false LIMIT 1""",
            new { slug = hospitalB.Slug });
        var patientAId = await conn.QueryFirstOrDefaultAsync<Guid>(
            """
            SELECT "Id" FROM "Patients"
            WHERE "TenantId" = @tenantId AND "Name" = @name AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId = tenantAId, name = hospitalA.ExclusivePatientName });

        return (tenantAId, tenantBId, patientAId);
    }
}

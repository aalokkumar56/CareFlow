using CureFlow.Application.Common;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Npgsql;

namespace CureFlow.UnitTests.Helpers;

/// <summary>Ensures multi-hospital E2E seed rows exist for DB-backed isolation tests.</summary>
internal static class TestSeedHelper
{
    public static async Task EnsureMultiHospitalAsync(string connectionString)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var session = new CureFlowDbSession(dataSource, new CurrentTenant());
        var hasher = new BcryptPasswordHasher();

        await RbacSeeder.SeedAsync(session);
        await MultiHospitalE2eSeeder.SeedAsync(session, hasher);

        await using var conn = await dataSource.OpenConnectionAsync();
        await using var tenantsCmd = new NpgsqlCommand(
            """
            SELECT COUNT(*)::int FROM "Tenants"
            WHERE "Slug" = ANY(@slugs) AND "IsDeleted" = false
            """,
            conn);
        tenantsCmd.Parameters.AddWithValue(
            "slugs",
            MultiHospitalE2eSeeder.Hospitals.Select(h => h.Slug).ToArray());
        var tenantCount = (int)(await tenantsCmd.ExecuteScalarAsync() ?? 0);
        tenantCount.Should().Be(
            MultiHospitalE2eSeeder.Hospitals.Count,
            "MultiHospitalE2eSeeder must leave three hospital tenants in the test database");

        foreach (var hospital in MultiHospitalE2eSeeder.Hospitals)
        {
            await using var patientCmd = new NpgsqlCommand(
                """
                SELECT COUNT(*)::int FROM "Patients" p
                INNER JOIN "Tenants" t ON t."Id" = p."TenantId"
                WHERE t."Slug" = @slug AND p."Name" = @name AND p."IsDeleted" = false
                """,
                conn);
            patientCmd.Parameters.AddWithValue("slug", hospital.Slug);
            patientCmd.Parameters.AddWithValue("name", hospital.ExclusivePatientName);
            var patientCount = (int)(await patientCmd.ExecuteScalarAsync() ?? 0);
            patientCount.Should().BeGreaterThan(
                0,
                "seed must include exclusive patient {0} for {1}",
                hospital.ExclusivePatientName,
                hospital.Slug);
        }
    }
}

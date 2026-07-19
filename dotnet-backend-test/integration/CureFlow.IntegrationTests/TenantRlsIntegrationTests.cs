using CureFlow.Infrastructure.Persistence;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// PostgreSQL RLS smoke for the integration host database.
/// Uses the same non-bypass role pattern as unit <c>TenantRlsIsolationTests</c>.
/// </summary>
[Collection("Database")]
public class TenantRlsIntegrationTests
{
    private const string RlsTestUser = "cureflow_rls_test";
    private const string RlsTestPassword = "CureFlowRlsTest_LocalOnly";

    [Fact]
    public async Task Rls_UnscopedRead_ReturnsZeroRows_ForNonBypassRole()
    {
        var adminCs = IntegrationTestHelpers.RequireConnectionString();
        EnsureRlsTestRole(adminCs);

        var rlsCs = new NpgsqlConnectionStringBuilder(adminCs)
        {
            Username = RlsTestUser,
            Password = RlsTestPassword,
        }.ConnectionString;

        await using var conn = new NpgsqlConnection(rlsCs);
        await conn.OpenAsync();

        (await PostgresRlsSession.CurrentRoleBypassesRlsAsync(conn))
            .Should().BeFalse("integration RLS role must not bypass row-level security");

        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(*)::int FROM "Patients" WHERE "IsDeleted" = false""",
            conn);
        var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);

        count.Should().Be(0, "RLS must block unscoped patient reads");
    }

    private static void EnsureRlsTestRole(string adminConnectionString)
    {
        using var conn = new NpgsqlConnection(adminConnectionString);
        conn.Open();

        using (var create = conn.CreateCommand())
        {
            create.CommandText =
                $"""
                DO $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{RlsTestUser}') THEN
                    CREATE ROLE {RlsTestUser} LOGIN PASSWORD '{RlsTestPassword}'
                      NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS INHERIT;
                  ELSE
                    ALTER ROLE {RlsTestUser} WITH LOGIN PASSWORD '{RlsTestPassword}'
                      NOSUPERUSER NOBYPASSRLS;
                  END IF;
                END $$;
                """;
            create.ExecuteNonQuery();
        }

        using var grants = conn.CreateCommand();
        grants.CommandText =
            $"""
            GRANT CONNECT ON DATABASE {conn.Database} TO {RlsTestUser};
            GRANT USAGE ON SCHEMA public TO {RlsTestUser};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {RlsTestUser};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {RlsTestUser};
            """;
        grants.ExecuteNonQuery();
    }
}

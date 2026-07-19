using Npgsql;

namespace CureFlow.UnitTests.Helpers;

internal static class TestDbHelper
{
    public const string NoDatabaseReason =
        "Test database is not configured. Set CUREFLOW_TEST_CONNECTION or ConnectionStrings:Default in CureFlow.Api appsettings.";

    /// <summary>Non-superuser role used so PostgreSQL RLS policies are actually enforced.</summary>
    public const string RlsTestUser = "cureflow_rls_test";

    public const string RlsTestPassword = "CureFlowRlsTest_LocalOnly";

    public static string? ResolveConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("CUREFLOW_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        // From unit/CureFlow.UnitTests/bin/{Config}/netX.Y → repo root → Api
        var apiDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "dotnet-backend", "src", "CureFlow.Api"));

        foreach (var fileName in new[] { "appsettings.Development.json", "appsettings.json" })
        {
            var cs = TryReadDefaultConnection(Path.Combine(apiDir, fileName));
            if (cs is not null)
                return cs;
        }

        return null;
    }

    public static string RequireConnectionString()
    {
        var cs = ResolveConnectionString();
        if (cs is null)
            throw new InvalidOperationException(NoDatabaseReason);

        try
        {
            using var conn = new NpgsqlConnection(cs);
            conn.Open();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{NoDatabaseReason} Open failed: {ex.Message}", ex);
        }

        return cs;
    }

    /// <summary>
    /// Connection string for a LOGIN role that does not bypass RLS.
    /// Creates/grants the role once via the admin connection when needed.
    /// </summary>
    public static string RequireRlsSubjectConnectionString()
    {
        var adminCs = RequireConnectionString();
        EnsureRlsTestRole(adminCs);

        var builder = new NpgsqlConnectionStringBuilder(adminCs)
        {
            Username = RlsTestUser,
            Password = RlsTestPassword,
        };
        return builder.ConnectionString;
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

        using (var grants = conn.CreateCommand())
        {
            grants.CommandText =
                $"""
                GRANT CONNECT ON DATABASE {conn.Database} TO {RlsTestUser};
                GRANT USAGE ON SCHEMA public TO {RlsTestUser};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {RlsTestUser};
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {RlsTestUser};
                ALTER DEFAULT PRIVILEGES IN SCHEMA public
                  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {RlsTestUser};
                """;
            grants.ExecuteNonQuery();
        }
    }

    private static string? TryReadDefaultConnection(string appsettingsPath)
    {
        if (!File.Exists(appsettingsPath))
            return null;

        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(appsettingsPath));
            var cs = json.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("Default")
                .GetString();

            if (string.IsNullOrWhiteSpace(cs) || cs.Contains("REPLACE-WITH", StringComparison.OrdinalIgnoreCase))
                return null;

            return cs;
        }
        catch
        {
            return null;
        }
    }
}

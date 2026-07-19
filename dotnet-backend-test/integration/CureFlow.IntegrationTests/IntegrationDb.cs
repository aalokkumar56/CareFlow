using Npgsql;

namespace CureFlow.IntegrationTests;

/// <summary>Resolves PostgreSQL for API host startup and seed-backed suites.</summary>
internal static class IntegrationDb
{
    private static readonly Lazy<string?> ConnectionString = new(ResolveAndProbe);

    public static string? ResolveConnectionString() => ConnectionString.Value;

    private static string? ResolveAndProbe()
    {
        var candidate = ReadConfiguredConnectionString();
        if (candidate is null)
            return null;

        try
        {
            using var conn = new NpgsqlConnection(candidate);
            conn.Open();
            return candidate;
        }
        catch
        {
            // Misconfigured local credentials — suites should skip, not fail host startup.
            return null;
        }
    }

    private static string? ReadConfiguredConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("CUREFLOW_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        // bin/{Config}/netX.Y → CureFlow.IntegrationTests → integration → dotnet-backend-test → repo
        var apiDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "dotnet-backend", "src", "CureFlow.Api"));

        foreach (var fileName in new[] { "appsettings.Development.json", "appsettings.json" })
        {
            var path = Path.Combine(apiDir, fileName);
            var cs = TryReadDefaultConnection(path);
            if (cs is not null)
                return cs;
        }

        return null;
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

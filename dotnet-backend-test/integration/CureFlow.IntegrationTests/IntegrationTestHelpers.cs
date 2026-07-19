using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CureFlow.IntegrationTests;

/// <summary>Shared helpers for API-host integration tests that need PostgreSQL.</summary>
internal static class IntegrationTestHelpers
{
    public const string NoDatabaseReason =
        "Test database is not configured (ConnectionStrings:Default / CUREFLOW_TEST_CONNECTION).";

    public static string? ResolveTestConnectionString() => IntegrationDb.ResolveConnectionString();

    public static string RequireConnectionString()
    {
        var cs = ResolveTestConnectionString();
        if (cs is null)
            throw new InvalidOperationException(NoDatabaseReason);
        return cs;
    }

    /// <summary>Fails the test when no usable PostgreSQL connection is available.</summary>
    public static void RequireDatabase(bool hasDatabase)
    {
        if (!hasDatabase)
            throw new InvalidOperationException(NoDatabaseReason);
    }

    /// <summary>Builds a configured test host; requires a reachable PostgreSQL database.</summary>
    public static WebApplicationFactory<Program> CreateHost(
        CustomWebApplicationFactory factory,
        IDictionary<string, string?>? extraConfig = null)
    {
        var connectionString = RequireConnectionString();

        var config = new Dictionary<string, string?>
        {
            // Auth bootstrap reads ASPNETCORE_ENVIRONMENT from IConfiguration (not IHostEnvironment).
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["ConnectionStrings:Default"] = connectionString,
        };

        foreach (var (key, value) in TestJwtHelper.JwtConfigOverrides())
            config[key] = value;

        if (extraConfig is not null)
        {
            foreach (var (key, value) in extraConfig)
                config[key] = value;
        }

        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
            builder.UseSetting("Jwt:Secret", TestJwtHelper.Secret);
            builder.UseSetting("Jwt:Issuer", TestJwtHelper.Issuer);
            builder.UseSetting("Jwt:Audience", TestJwtHelper.Audience);
            builder.UseSetting("ConnectionStrings:Default", connectionString);

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(config);
            });
        });
    }

    /// <summary>Obsolete name kept as alias so call sites can be updated incrementally.</summary>
    public static WebApplicationFactory<Program> CreateHostOrSkip(
        CustomWebApplicationFactory factory,
        IDictionary<string, string?>? extraConfig = null) =>
        CreateHost(factory, extraConfig);
}

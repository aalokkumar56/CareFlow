using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>Shared helpers for API-host integration tests that need PostgreSQL.</summary>
internal static class IntegrationTestHelpers
{
    public const string NoDatabaseReason = "Test database is not configured (ConnectionStrings:Default / CUREFLOW_TEST_DB).";

    public static string? ResolveTestConnectionString() => IntegrationDb.ResolveConnectionString();

    /// <summary>Skip the current [SkippableFact] when no usable PostgreSQL connection is available.</summary>
    public static void RequireDatabase(bool hasDatabase) =>
        Skip.If(!hasDatabase, NoDatabaseReason);

    /// <summary>
    /// Builds a configured test host, or skips the test when the database is unavailable.
    /// Requires the calling test to be marked <see cref="SkippableFactAttribute"/>.
    /// </summary>
    public static WebApplicationFactory<Program> CreateHostOrSkip(
        CustomWebApplicationFactory factory,
        IDictionary<string, string?>? extraConfig = null)
    {
        var connectionString = ResolveTestConnectionString();
        Skip.If(connectionString is null, NoDatabaseReason);

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
}

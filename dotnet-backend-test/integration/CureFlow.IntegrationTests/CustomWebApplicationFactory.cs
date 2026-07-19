using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Shared test host for API integration tests (Microsoft WebApplicationFactory pattern).
/// Override configuration / services in derived factories as suites grow.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Stable JWT secret for integration tokens (must match <see cref="TestJwtHelper.Secret"/>).</summary>
    public const string TestJwtSecret = TestJwtHelper.Secret;

    /// <summary>True when a usable PostgreSQL connection string is available.</summary>
    public bool HasDatabase => IntegrationDb.ResolveConnectionString() is not null;

    static CustomWebApplicationFactory()
    {
        // WebApplication.CreateBuilder reads process env early; ensure JWT bootstrap
        // never sees appsettings REPLACE-WITH placeholders under ASPNETCORE_ENVIRONMENT=Production.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtHelper.Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtHelper.Audience);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = IntegrationDb.ResolveConnectionString();

        // UseSetting wins over appsettings REPLACE-WITH placeholders for early bootstrap.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Jwt:Issuer", TestJwtHelper.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwtHelper.Audience);
        // Always set a connection string so appsettings placeholders cannot win.
        builder.UseSetting(
            "ConnectionStrings:Default",
            connectionString ?? "Host=127.0.0.1;Port=1;Database=cureflow_unavailable;Username=none;Password=none");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Do NOT AddJsonFile(appsettings.Development.json): its Jwt:Secret contains
            // "REPLACE-WITH..." and can win over UseSetting when host sources reorder.
            var overrides = new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["Database:AutoMigrate"] = "false",
                ["Database:Seed"] = "false",
                ["Database:SeedPlatformUser"] = "false",
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:Issuer"] = TestJwtHelper.Issuer,
                ["Jwt:Audience"] = TestJwtHelper.Audience,
                ["ConnectionStrings:Default"] = connectionString
                    ?? "Host=127.0.0.1;Port=1;Database=cureflow_unavailable;Username=none;Password=none",
            };

            // Add last so values beat appsettings*.json loaded by the host.
            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureTestServices(services =>
        {
            // Avoid background schedulers during API contract tests.
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                services.Remove(descriptor);
        });
    }

    protected static void ReplaceService<TService>(IServiceCollection services, TService implementation)
        where TService : class
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(TService)).ToList())
            services.Remove(descriptor);
        services.AddSingleton(implementation);
    }
}

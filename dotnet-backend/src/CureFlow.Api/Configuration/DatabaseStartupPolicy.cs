using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CureFlow.Api.Configuration;

/// <summary>Runtime hosts never provision or mutate a non-development schema.</summary>
public sealed record DatabaseStartupPolicy(bool AutoMigrate, bool SeedCatalog, bool SeedDemo,
    bool SeedPlatformUser, bool SeedTestPhone)
{
    public bool RequiresInitialization => AutoMigrate || SeedCatalog || SeedDemo;

    public static DatabaseStartupPolicy Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var development = environment.IsDevelopment();
        var migrate = configuration.GetValue("Database:AutoMigrate", false);
        var catalog = configuration.GetValue("Database:SeedCatalog", development);
        var demo = configuration.GetValue("Database:Seed", false);
        var platform = configuration.GetValue("Database:SeedPlatformUser", false);
        var testPhone = configuration.GetValue("WhatsApp:UseTestPhone", false);

        if (!development && (migrate || catalog || demo || platform || testPhone))
            throw new InvalidOperationException(
                "Database initialization and demo seeding are allowed only in Development. " +
                "Set Database:AutoMigrate, Database:SeedCatalog, Database:Seed, " +
                "Database:SeedPlatformUser and WhatsApp:UseTestPhone to false. " +
                "Use a separate reviewed migration/provisioning job for staging and production.");

        if ((platform || testPhone) && !demo)
            throw new InvalidOperationException(
                "Database:SeedPlatformUser and WhatsApp:UseTestPhone require explicit Development demo seeding.");

        if (!development)
        {
            foreach (var name in new[] { "Default", "Identity", "Catalogue", "PlatformIdentity", "ControlPlane" })
            {
                var connection = configuration.GetConnectionString(name);
                if (string.IsNullOrWhiteSpace(connection)) continue;
                var normalized = CureFlow.Infrastructure.Persistence.Dapper.CureFlowNpgsqlDataSource.Normalize(connection);
                if (new Npgsql.NpgsqlConnectionStringBuilder(normalized).SslMode != Npgsql.SslMode.VerifyFull)
                    throw new InvalidOperationException(
                        "Non-development runtime PostgreSQL connections require SSL Mode=VerifyFull.");
            }
        }

        return new(migrate, catalog, demo, platform, testPhone);
    }
}

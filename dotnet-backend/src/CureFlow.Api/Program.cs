using CureFlow.Api.Extensions;
using CureFlow.Api.Configuration;
using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Containers (Render, Docker) share a low inotify limit. Watching appsettings*.json
// for live reload creates FileSystemWatchers and can crash startup with:
// "The configured user limit (128) on the number of inotify instances has been reached".
// Prefer env vars for production config; disable file reload before host build.
if (string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "true",
        StringComparison.OrdinalIgnoreCase)
    || string.Equals(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        Environments.Production,
        StringComparison.OrdinalIgnoreCase))
{
    Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
}

var builder = WebApplication.CreateBuilder(args);
var startupPolicy = DatabaseStartupPolicy.Read(builder.Configuration, builder.Environment);

builder.Logging.ClearProviders();
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddCureFlowApi(builder.Configuration, builder.Environment);
builder.Services.AddCureFlowInfrastructure(builder.Configuration);
builder.Services.AddCureFlowAuthentication(builder.Configuration);

builder.Services.AddScoped<CureFlow.Infrastructure.Areas.Platform.PlatformAccess>();
builder.Services.AddScoped<CureFlow.Infrastructure.Areas.Platform.PlatformAdminService>();
builder.Services.AddScoped<CureFlow.Infrastructure.Areas.Platform.TenantApiActivityService>();
var app = builder.Build();
app.UseCureFlowRequestPipeline();

if (startupPolicy.RequiresInitialization)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    if (startupPolicy.AutoMigrate)
    {
        // Even local migrations use an explicitly selected maintenance connection.
        var migrationConnection = builder.Configuration.GetConnectionString("Migration")
            ?? throw new InvalidOperationException("Connection string 'Migration' is required for database initialization.");
        var migrationOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(CureFlow.Infrastructure.Persistence.Dapper.CureFlowNpgsqlDataSource.Normalize(migrationConnection))
            .Options;
        await using var db = new ApplicationDbContext(migrationOptions,
            new CureFlow.Application.Common.CurrentTenant());
        await MigrateWithRetryAsync(db, logger);
    }

    if (startupPolicy.SeedCatalog || startupPolicy.SeedDemo)
    {
        var session = scope.ServiceProvider.GetRequiredService<IProvisioningDbSession>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Non-development catalog updates belong to the reviewed provisioning job.
        if (startupPolicy.SeedCatalog)
        {
            await RbacSeeder.SeedAsync(session);
            await CureFlow.Infrastructure.Areas.Platform.PlatformSeeder.SeedAsync(session);
        }

        // Demo / E2E data — local/dev only. Production uses one-time /platform/auth/bootstrap.
        if (startupPolicy.SeedDemo)
        {
            await DemoSeeder.SeedAsync(session, hasher);
            await MultiHospitalE2eSeeder.SeedAsync(session, hasher);

            // The default operator is an explicit Development demo-data opt-in.
            if (startupPolicy.SeedPlatformUser)
            {
                await PlatformUserSeeder.SeedAsync(session, hasher);
            }

            if (startupPolicy.SeedTestPhone)
            {
                var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("TestPhoneSeeder");
                var scopeSlug = builder.Configuration["WhatsApp:TestPhoneTenantSlug"];
                await TestPhoneSeeder.SeedAsync(session, seedLogger, scopeSlug);
            }
        }
    }
}

app.Run();

static async Task MigrateWithRetryAsync(ApplicationDbContext db, Microsoft.Extensions.Logging.ILogger logger)
{
    const int maxAttempts = 5;
    Exception? lastEx = null;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts && IsTransientDbFailure(ex))
        {
            lastEx = ex;
            var delay = TimeSpan.FromSeconds(Math.Min(30, 5 * attempt));
            logger.LogWarning(
                ex,
                "Database migrate attempt {Attempt}/{Max} failed (Neon cold start?). Retrying in {Delay}s…",
                attempt,
                maxAttempts,
                delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
    // BUG-08: all retry attempts exhausted — fail fast rather than starting with an unmigrated DB
    logger.LogCritical(lastEx, "Database migration failed after {Max} attempts. Shutting down.", maxAttempts);
    throw lastEx ?? new InvalidOperationException("Database migration failed.");
}

static bool IsTransientDbFailure(Exception ex)
{
    for (var e = ex; e != null; e = e.InnerException!)
    {
        if (e is TimeoutException or IOException)
            return true;
        if (e is Npgsql.NpgsqlException npg
            && (npg.IsTransient || npg.InnerException is TimeoutException or IOException))
            return true;
        if (e.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("reading from stream", StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("Exception while reading", StringComparison.OrdinalIgnoreCase))
            return true;
    }

    return false;
}

using CureFlow.Api.Extensions;
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

builder.Logging.ClearProviders();
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddCureFlowApi(builder.Configuration);
builder.Services.AddCureFlowInfrastructure(builder.Configuration);
builder.Services.AddCureFlowAuthentication(builder.Configuration);

var app = builder.Build();
app.UseCureFlowRequestPipeline();

await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var session = scope.ServiceProvider.GetRequiredService<ICureFlowDbSession>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    if (builder.Configuration.GetValue("Database:AutoMigrate", defaultValue: true))
    {
        await MigrateWithRetryAsync(db, logger);
    }

    // Permissions catalog — lightweight, required in every environment.
    await RbacSeeder.SeedAsync(session);

    // Demo / E2E data — local/dev only. Production uses one-time /platform/auth/bootstrap.
    if (builder.Configuration.GetValue("Database:Seed", defaultValue: false)
        && !app.Environment.IsProduction())
    {
        await DemoSeeder.SeedAsync(session, hasher);
        await RbacSeeder.SeedAsync(session);
        await MultiHospitalE2eSeeder.SeedAsync(session, hasher);
        await RbacSeeder.SeedAsync(session);

        // Platform owner is normally created once via the /platform/login setup form
        // (bootstrap API). Opt in to seeding the default ops account for automated
        // tests by setting Database:SeedPlatformUser=true.
        if (builder.Configuration.GetValue("Database:SeedPlatformUser", defaultValue: false))
        {
            await PlatformUserSeeder.SeedAsync(session, hasher);
        }

        if (builder.Configuration.GetValue("WhatsApp:UseTestPhone", defaultValue: false))
        {
            var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("TestPhoneSeeder");
            var scopeSlug = builder.Configuration["WhatsApp:TestPhoneTenantSlug"];
            await TestPhoneSeeder.SeedAsync(session, seedLogger, scopeSlug);
        }
    }
}

app.Run();

static async Task MigrateWithRetryAsync(ApplicationDbContext db, Microsoft.Extensions.Logging.ILogger logger)
{
    const int maxAttempts = 5;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts && IsTransientDbFailure(ex))
        {
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

using CureFlow.Api.Extensions;
using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

    // Demo / E2E / test-phone data — local/dev only. Keep off on Render/Neon.
    if (builder.Configuration.GetValue("Database:Seed", defaultValue: false)
        && !app.Environment.IsProduction())
    {
        await DemoSeeder.SeedAsync(session, hasher);
        await PlatformUserSeeder.SeedAsync(session, hasher);
        await RbacSeeder.SeedAsync(session);
        await MultiHospitalE2eSeeder.SeedAsync(session, hasher);
        await RbacSeeder.SeedAsync(session);

        if (builder.Configuration.GetValue("WhatsApp:UseTestPhone", defaultValue: false))
        {
            var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("TestPhoneSeeder");
            var scopeSlug = builder.Configuration["WhatsApp:TestPhoneTenantSlug"];
            await TestPhoneSeeder.SeedAsync(session, seedLogger, scopeSlug);
        }
    }
    else if (builder.Configuration.GetValue("Database:SeedPlatformUsers", defaultValue: false))
    {
        // Optional: create platform ops user without demo hospital data.
        await PlatformUserSeeder.SeedAsync(session, hasher);
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

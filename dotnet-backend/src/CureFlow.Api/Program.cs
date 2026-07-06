using CureFlow.Api.Extensions;
using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.Persistence;
using CureFlow.Infrastructure.Persistence.Seeders;
using Serilog;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var apiLogDir = Path.Combine(builder.Environment.ContentRootPath, "Logs", "api");
Directory.CreateDirectory(apiLogDir);
var sessionLogFile = Path.Combine(apiLogDir, $"cureflow-api_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
var dailyLogFile = Path.Combine(apiLogDir, "cureflow-api-.log");
const string logTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

// builder.Host.UseSerilog((ctx, lc) => lc
//     .ReadFrom.Configuration(ctx.Configuration)
//     .MinimumLevel.Is(ctx.HostingEnvironment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
//     .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
//     .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
//     .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
//     .Enrich.FromLogContext()
//     .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
//     .WriteTo.File(sessionLogFile, outputTemplate: logTemplate, shared: true)
//     .WriteTo.File(
//         dailyLogFile,
//         rollingInterval: RollingInterval.Day,
//         retainedFileCountLimit: 90,
//         outputTemplate: logTemplate,
//         shared: true));
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
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
       // await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    var session = scope.ServiceProvider.GetRequiredService<ICureFlowDbSession>();
    await RbacSeeder.SeedAsync(session);

    if (builder.Configuration.GetValue<bool>("Database:Seed"))
    {
        await DemoSeeder.SeedAsync(session, hasher);
        await RbacSeeder.SeedAsync(session);

        if (builder.Configuration.GetValue<bool>("WhatsApp:UseTestPhone"))
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("TestPhoneSeeder");
            await TestPhoneSeeder.SeedAsync(session, logger);
        }
    }
}

app.Run();

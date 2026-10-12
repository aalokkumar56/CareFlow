using CureFlow.Application.Common;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CureFlow.Infrastructure.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var candidatePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "CureFlow.Api"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "CureFlow.Api"),
            Directory.GetCurrentDirectory(),
        };
        var basePath = candidatePaths.FirstOrDefault(candidate =>
            File.Exists(Path.Combine(candidate, "CureFlow.Api.csproj")))
            ?? throw new InvalidOperationException("Run migration tooling from the backend root or an API/Infrastructure project directory.");
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        if (environment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("Invalid migration environment name.");
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = CureFlowNpgsqlDataSource.Normalize(
            config.GetConnectionString("Migration")
            ?? throw new InvalidOperationException("Connection string 'Migration' is required. Migration tooling cannot fall back to runtime credentials."));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options, new CurrentTenant());
    }
}

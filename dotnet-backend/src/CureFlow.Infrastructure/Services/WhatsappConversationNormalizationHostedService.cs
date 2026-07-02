using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services;

public class WhatsappConversationNormalizationHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WhatsappConversationNormalizationHostedService> _logger;

    public WhatsappConversationNormalizationHostedService(
        IServiceProvider services,
        ILogger<WhatsappConversationNormalizationHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ICureFlowDbSession>();

            var defaultTenantId = await db.QueryFirstOrDefaultAsync<Guid>(
                """
                SELECT "Id" FROM "Tenants"
                WHERE "IsActive" = true AND "IsDeleted" = false
                ORDER BY "CreatedAt"
                LIMIT 1
                """,
                ignoreTenant: true,
                ct: stoppingToken);

            if (defaultTenantId == Guid.Empty)
            {
                _logger.LogInformation("Skipping WhatsApp conversation normalization — no active tenant");
                return;
            }

            _logger.LogInformation("Starting deferred WhatsApp conversation normalization");
            await WhatsappConversationHelper.NormalizeAndMergeAsync(db, defaultTenantId, stoppingToken);
            _logger.LogInformation("WhatsApp conversation normalization completed");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp conversation normalization failed");
        }
    }
}

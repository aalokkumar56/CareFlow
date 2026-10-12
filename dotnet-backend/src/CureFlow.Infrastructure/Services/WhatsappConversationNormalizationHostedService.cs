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
            await CureFlow.Infrastructure.Persistence.TenantScopeRunner.RunForAllTenantsAsync(
                _services, (ICureFlowDbSession db, CureFlow.Domain.Entities.Saas.Tenant tenant) =>
                    WhatsappConversationHelper.NormalizeAndMergeAsync(db, tenant.Id, stoppingToken), stoppingToken);
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

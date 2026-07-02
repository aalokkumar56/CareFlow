using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services;

public class CampaignSchedulerHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CampaignSchedulerHostedService> _logger;

    public CampaignSchedulerHostedService(IServiceProvider services, ILogger<CampaignSchedulerHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Campaign scheduler started");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessDueCampaignsAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Campaign scheduler tick failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task ProcessDueCampaignsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await TenantScopeRunner.RunForAllTenantsAsync(_services, async (db, tenant) =>
        {
            var dueIds = await db.QueryAsync<Guid>(
                """
                SELECT "Id" FROM "Campaigns"
                WHERE "Status" = @status AND "ScheduledAt" IS NOT NULL AND "ScheduledAt" <= @now
                  AND "IsDeleted" = false AND "TenantId" = @TenantId
                """,
                new { status = (int)CampaignStatus.Scheduled, now },
                ct: ct);

            if (dueIds.Count == 0) return;

            using var tenantScope = _services.CreateScope();
            var ctx = (CurrentTenant)tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();
            ctx.TenantId = tenant.Id;
            ctx.IsAuthenticated = true;
            ctx.UserEmail = "system@campaign-scheduler";

            var campaignService = tenantScope.ServiceProvider.GetRequiredService<ICampaignService>();
            foreach (var id in dueIds)
            {
                try
                {
                    var (sent, failed, total) = await campaignService.SendAsync(id, ct);
                    _logger.LogInformation(
                        "Scheduled campaign {CampaignId} for tenant {TenantId}: sent={Sent} failed={Failed} total={Total}",
                        id, tenant.Id, sent, failed, total);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send scheduled campaign {CampaignId} for tenant {TenantId}.", id, tenant.Id);
                }
            }
        }, ct);
    }
}

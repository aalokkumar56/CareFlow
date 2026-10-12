using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace CureFlow.Infrastructure.Outbox;

public sealed class OutboxHostedWorker(IServiceProvider services, IOptions<OutboxOptions> options, ILogger<OutboxHostedWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        options.Value.Validate();
        using (var validation = services.CreateScope())
            if (!validation.ServiceProvider.GetServices<ITransactionalOutboxConsumer>().Any())
                throw new InvalidOperationException("Outbox activation requires an explicitly registered approved local consumer.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TenantScopeRunner.RunForAllTenantsAsync(services, async (IServiceProvider scoped, CureFlow.Domain.Entities.Saas.Tenant _) =>
                    await scoped.GetRequiredService<OutboxDispatcher>().RunBatchAsync(stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Outbox polling failed; durable events retained for the next bounded attempt."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}

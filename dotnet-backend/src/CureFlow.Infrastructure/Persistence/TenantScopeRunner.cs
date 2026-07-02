using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using Microsoft.Extensions.DependencyInjection;

using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Persistence;

/// <summary>Runs an action for each active tenant with tenant context set.</summary>
public static class TenantScopeRunner
{
    public static async Task RunForAllTenantsAsync(
        IServiceProvider services,
        Func<ICureFlowDbSession, Tenant, Task> action,
        CancellationToken ct = default) =>
        await RunForAllTenantsAsync(services, (sp, tenant) =>
            action(sp.GetRequiredService<ICureFlowDbSession>(), tenant), ct);

    public static async Task RunForAllTenantsAsync(
        IServiceProvider services,
        Func<IServiceProvider, Tenant, Task> action,
        CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<ICureFlowDbSession>();
        var tenants = await bootstrap.QueryAsync<Tenant>(
            """
            SELECT * FROM "Tenants"
            WHERE "IsActive" = true AND "IsDeleted" = false
            """,
            ignoreTenant: true,
            ct: ct);

        foreach (var tenant in tenants)
        {
            using var tenantScope = services.CreateScope();
            var ctx = (CurrentTenant)tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();
            ctx.TenantId = tenant.Id;
            ctx.IsAuthenticated = true;
            await action(tenantScope.ServiceProvider, tenant);
        }
    }
}

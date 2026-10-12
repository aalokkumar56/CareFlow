using CureFlow.Application.Areas.Platform;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
namespace CureFlow.Infrastructure.Areas.Platform;
public sealed record EffectiveModule(Guid ModuleId, string Key, string Name, bool IsCore, bool IsActive, bool IsEnabled, bool Effective, bool PlanEnabled, string Source, int? Limit, DateTime? ExpiresAt, int Version);
public static class PlatformModuleAccess
{
    public static async Task<IReadOnlyList<EffectiveModule>> ResolveAsync(ICureFlowDbSession db, Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await db.GetByIdAsync<Tenant>(tenantId, true, ct);
        if (tenant is null || tenant.IsDeleted) throw new DomainException("Tenant not found.",404);
        var modules = await db.GetAllAsync<PlatformModule>(ignoreTenant:true, ct:ct);
        var grants = await db.QueryAsync<TenantEntitlement>("SELECT * FROM \"TenantEntitlements\" WHERE \"TenantId\"=@tenantId AND NOT \"IsDeleted\"",new{tenantId},true,ct);
        var defaults = await db.QueryAsync<PlanModule>("SELECT * FROM \"PlanModules\" WHERE \"Plan\"=@plan AND NOT \"IsDeleted\"",new{plan=(int)tenant.Plan},true,ct);
        var now = DateTime.UtcNow;
        var subscriptionAllowsAccess = tenant.SubscriptionStatus is not SubscriptionStatus.Cancelled and not SubscriptionStatus.Expired
            && (tenant.SubscriptionStatus != SubscriptionStatus.Trialing || tenant.TrialEndsAt is null || tenant.TrialEndsAt > now);
        return Evaluate(modules, grants, defaults, now, subscriptionAllowsAccess);
    }
    public static IReadOnlyList<EffectiveModule> Evaluate(IEnumerable<PlatformModule> modules, IEnumerable<TenantEntitlement> grants, IEnumerable<PlanModule> defaults, DateTime now, bool subscriptionAllowsAccess = true)
    {
        var catalogue=modules.ToArray();var overrides=grants.ToArray();var policies=defaults.ToArray();
        var evaluated = catalogue.Where(m=>!m.IsDeleted && SupportedModules.All.Any(s=>s.Key==m.Key)).Select(m=>{
            var matches=overrides.Where(g=>g.PlatformModuleId==m.Id&&!g.IsDeleted).ToArray();
            var plans=policies.Where(p=>p.PlatformModuleId==m.Id&&!p.IsDeleted).ToArray();
            var grant=matches.FirstOrDefault();
            var planEnabled=plans.Length==0?m.IsCore:plans.Length==1&&plans[0].IsActive&&plans[0].IsEnabled;
            // A commercial plan is not an explicit clinical-service activation decision.
            var requiresClinicalGrant=m.Key is "clinical" or "appointments";
            var enabled=grant?.IsEnabled??(!requiresClinicalGrant&&planEnabled);
            return new EffectiveModule(m.Id,m.Key,m.Name,m.IsCore,m.IsActive,enabled,
                subscriptionAllowsAccess&&m.IsActive&&enabled&&matches.Length<=1&&(grant?.IsActive??true)
                    &&catalogue.Count(x=>x.Key==m.Key&&!x.IsDeleted)==1&&(grant?.ExpiresAt is null || grant.ExpiresAt>now),
                planEnabled,grant is null?"plan":"tenant",grant?.Limit,grant?.ExpiresAt,grant?.Version??0);
        }).ToArray();
        // Propagate denied dependencies without silently enabling another module or changing persisted customer choices.
        for(var pass=0;pass<evaluated.Length;pass++)
            evaluated=evaluated.Select(m=>m.Effective&&SupportedModules.Dependencies(m.Key).Any(key=>!evaluated.Any(x=>x.Key==key&&x.Effective))
                ?m with {Effective=false}:m).ToArray();
        return evaluated;
    }
}

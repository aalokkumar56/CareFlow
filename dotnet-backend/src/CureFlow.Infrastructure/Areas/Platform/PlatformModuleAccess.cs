using CureFlow.Application.Areas.Platform;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
namespace CureFlow.Infrastructure.Areas.Platform;
public sealed record EffectiveModule(Guid ModuleId, string Key, string Name, bool IsCore, bool IsActive, bool IsEnabled, bool Effective, bool PlanEnabled, string Source, int? Limit, DateTime? ExpiresAt, int Version);
public static class PlatformModuleAccess
{
    public static async Task<IReadOnlyList<EffectiveModule>> ResolveAsync(ICureFlowDbSession db, Guid tenantId)
    {
        var tenant = await db.GetByIdAsync<Tenant>(tenantId, true);
        if (tenant is null || tenant.IsDeleted) throw new DomainException("Tenant not found.",404);
        var modules = await db.GetAllAsync<PlatformModule>(ignoreTenant:true);
        var grants = await db.QueryAsync<TenantEntitlement>("SELECT * FROM \"TenantEntitlements\" WHERE \"TenantId\"=@tenantId AND NOT \"IsDeleted\"",new{tenantId},true);
        var defaults = await db.QueryAsync<PlanModule>("SELECT * FROM \"PlanModules\" WHERE \"Plan\"=@plan AND NOT \"IsDeleted\"",new{plan=(int)tenant.Plan},true);
        return Evaluate(modules, grants, defaults, DateTime.UtcNow);
    }
    public static IReadOnlyList<EffectiveModule> Evaluate(IEnumerable<PlatformModule> modules, IEnumerable<TenantEntitlement> grants, IEnumerable<PlanModule> defaults, DateTime now)
    {
        return modules.Where(m=>!m.IsDeleted && SupportedModules.All.Any(s=>s.Key==m.Key)).Select(m=>{
            var grant=grants.FirstOrDefault(g=>g.PlatformModuleId==m.Id&&!g.IsDeleted);
            var planEnabled=defaults.FirstOrDefault(p=>p.PlatformModuleId==m.Id&&!p.IsDeleted)?.IsEnabled??m.IsCore;
            var enabled=grant?.IsEnabled??planEnabled;
            return new EffectiveModule(m.Id,m.Key,m.Name,m.IsCore,m.IsActive,enabled,
                m.IsActive&&enabled&&(grant?.ExpiresAt is null || grant.ExpiresAt>now),
                planEnabled,grant is null?"plan":"tenant",grant?.Limit,grant?.ExpiresAt,grant?.Version??0);
        }).ToArray();
    }
}

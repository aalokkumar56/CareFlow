using CureFlow.Application.Areas.Platform;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using System.Text.Json;
namespace CureFlow.Infrastructure.Areas.Platform;
public static class PlatformSeeder
{
 public static async Task SeedAsync(ICureFlowDbSession db) {
  await db.TransactionAsync(async store=>{
   await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ignoreTenant:true);
   foreach(var key in new[]{"owner","administrator","support","billing","viewer"}){
    if(await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"PlatformRoles\" WHERE \"Key\"=@key",new{key},true)>0)continue;
    var permissions=key switch {
     "owner"=>PlatformPermissions.All,
     "administrator"=>PlatformPermissions.All.Where(p=>!p.StartsWith("roles.")&&!p.StartsWith("operators.")).ToArray(),
     "support"=>new[]{"overview.read","tenants.read","support.read","support.write","facilities.read","entitlements.read"},
     "billing"=>new[]{"overview.read","tenants.read","billing.read","billing.write"},
     _=>PlatformPermissions.All.Where(p=>p.EndsWith(".read")&&!p.StartsWith("support.")).ToArray()
    };
    await store.InsertAsync(new PlatformRole{Key=key,Name=char.ToUpper(key[0])+key[1..],PermissionsJson=JsonSerializer.Serialize(permissions)},true);
   }
   foreach(var definition in SupportedModules.All){
    var module=await store.QueryFirstOrDefaultAsync<PlatformModule>("SELECT * FROM \"PlatformModules\" WHERE \"Key\"=@Key",new{definition.Key},true);
    if(module is null){
     module=new PlatformModule{Key=definition.Key,Name=definition.Name,Description=definition.Description,IsCore=true};
     await store.InsertAsync(module,true);
     // Split the old grouped capabilities without losing explicit customer decisions.
     var legacy=definition.Key switch {"patients" or "leads" or "referrals"=>"crm","campaigns"=>"communications",_=>null};
     if(legacy is not null){
      var inherited=await store.QueryAsync<TenantEntitlement>("SELECT e.* FROM \"TenantEntitlements\" e JOIN \"PlatformModules\" m ON m.\"Id\"=e.\"PlatformModuleId\" WHERE m.\"Key\"=@legacy AND NOT e.\"IsDeleted\"",new{legacy},true);
      foreach(var old in inherited)await store.InsertAsync(new TenantEntitlement{TenantId=old.TenantId,PlatformModuleId=module.Id,IsEnabled=old.IsEnabled,Limit=old.Limit,ExpiresAt=old.ExpiresAt,Version=1},true);
     }
    }
    foreach(var plan in Enum.GetValues<CureFlow.Domain.Enums.SubscriptionPlan>()){
     var exists=await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"PlanModules\" WHERE \"Plan\"=@plan AND \"PlatformModuleId\"=@id",new{plan=(int)plan,id=module.Id},true);
     if(exists==0)await store.InsertAsync(new PlanModule{Plan=plan,PlatformModuleId=module.Id,IsEnabled=module.IsCore},true);
    }
   }
  }, ignoreTenant: true);
 }
}

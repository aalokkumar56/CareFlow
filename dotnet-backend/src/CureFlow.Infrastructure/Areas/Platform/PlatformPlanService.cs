using CureFlow.Application.Areas.Platform;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
namespace CureFlow.Infrastructure.Areas.Platform;
public sealed partial class PlatformAdminService
{
 public async Task<object> TenantModules(Guid tenant) {
  var modules=await PlatformModuleAccess.ResolveAsync(db,tenant);
  return new { EnabledKeys=modules.Where(m=>m.Effective).Select(m=>m.Key), Modules=modules };
 }
 public async Task<object> PlanModules() {
  var modules=(await db.GetAllAsync<PlatformModule>(ignoreTenant:true)).Where(m=>!m.IsDeleted&&SupportedModules.All.Any(s=>s.Key==m.Key)).ToArray();
  var policies=await db.GetAllAsync<PlanModule>(ignoreTenant:true);
  return Enum.GetValues<SubscriptionPlan>().Select(plan=>new {Plan=plan,Modules=modules.Select(m=>new {ModuleId=m.Id,m.Key,m.Name,IsEnabled=policies.FirstOrDefault(p=>p.Plan==plan&&p.PlatformModuleId==m.Id&&!p.IsDeleted)?.IsEnabled??m.IsCore})});
 }
 public Task<object> PlanModule(SubscriptionPlan plan,Guid module,PlanModuleInput input,string key)=>Change("billing.write","plan.module.save",null,key,new{plan,module,input},input.Reason,async(store,_)=>{
  if(!Enum.IsDefined(plan))throw Invalid("Select a valid subscription plan.");
  var capability=await Find<PlatformModule>(store,module);
  if(!SupportedModules.All.Any(s=>s.Key==capability.Key))throw Invalid("Only supported application modules can be assigned.");
  if(input.IsEnabled){
   var catalogue=await store.GetAllAsync<PlatformModule>(ignoreTenant:true);
   var defaults=await store.QueryAsync<PlanModule>("SELECT * FROM \"PlanModules\" WHERE \"Plan\"=@plan AND NOT \"IsDeleted\"",new{plan=(int)plan},true);
   var effective=PlatformModuleAccess.Evaluate(catalogue,Array.Empty<TenantEntitlement>(),defaults,DateTime.UtcNow);
   if(SupportedModules.Dependencies(capability.Key).Any(dependency=>!effective.Any(x=>x.Key==dependency&&x.Effective)))
    throw new CureFlow.Application.Common.DomainException("Enable the required plan module dependencies first.",409);
  }
  var row=await store.QueryFirstOrDefaultAsync<PlanModule>("SELECT * FROM \"PlanModules\" WHERE \"Plan\"=@plan AND \"PlatformModuleId\"=@module",new{plan=(int)plan,module},true);
  var before=row is null?null:Encode(row);var create=row is null;
  row??=new PlanModule{Plan=plan,PlatformModuleId=module};row.IsEnabled=input.IsEnabled;row.IsDeleted=false;
  if(create)await store.InsertAsync(row,true);else await store.UpdateAsync(row,true);
  return(before,row);
 });
 public Task<object> ResetEntitlement(Guid tenant,Guid module,PlatformChange input,string key)=>Change("entitlements.write","entitlement.reset",tenant,key,new{module,input},input.Reason,async(store,_)=>{
  await Find<PlatformModule>(store,module);
  var row=await store.QueryFirstOrDefaultAsync<TenantEntitlement>("SELECT * FROM \"TenantEntitlements\" WHERE \"TenantId\"=@tenant AND \"PlatformModuleId\"=@module",new{tenant,module},true);
  var before=row is null?null:Encode(row);
  if(row is not null){row.IsDeleted=true;row.Version++;await store.UpdateAsync(row,true);}
  return(before,new{ModuleId=module,Source="plan"});
 });
}

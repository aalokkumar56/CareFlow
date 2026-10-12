using CureFlow.Application.Areas.Platform;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using System.Text.Json;
namespace CureFlow.Infrastructure.Areas.Platform;
public sealed partial class PlatformAdminService
{
 public async Task<object> Modules()=> (await db.GetAllAsync<PlatformModule>(ignoreTenant:true)).Where(x=>!x.IsDeleted&&SupportedModules.All.Any(s=>s.Key==x.Key));
 public Task<object> Module(Guid? id,ModuleInput input,string key)=>Change("modules.write","module.save",null,key,new{id,input},input.Reason,async(store,_)=>{
  if(!id.HasValue || !SupportedModules.All.Any(s=>s.Key==input.Key))throw Invalid("Modules are registered by application releases. Choose an existing supported module.");
  Text(input.Name,"Name");var row=id.HasValue?await Find<PlatformModule>(store,id.Value):new PlatformModule(); var before=id.HasValue?Encode(row):null;
  if(id.HasValue&&input.Key!=row.Key)throw Invalid("Module keys cannot be renamed.");
  var duplicate=await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"PlatformModules\" WHERE \"Key\"=@Key AND \"Id\"<>@Id",new{input.Key,row.Id},true);
  if(duplicate>0)throw new DomainException("Module key already exists.",409);
  row.Key=input.Key;row.Name=input.Name.Trim();row.Description=input.Description;row.IsCore=input.IsCore;row.IsActive=input.IsActive;
  await Save(store,row,!id.HasValue);return(before,row);
 });
 public async Task<object> Facilities(Guid id) {
  await Find<Tenant>(db,id);
  return await db.QueryAsync<Facility>("SELECT * FROM \"Facilities\" WHERE \"TenantId\"=@id AND NOT \"IsDeleted\" ORDER BY \"IsPrimary\" DESC,\"Name\"",new{id},true);
 }
 public Task<object> Facility(Guid tenant,Guid? id,FacilityInput input,string key)=>Change("facilities.write","facility.save",tenant,key,new{id,input},input.Reason,async(store,actor)=>{
  Text(input.Name,"Name");
  var row=id.HasValue?await Find<Facility>(store,id.Value):new Facility{TenantId=tenant};var before=id.HasValue?Encode(row):null;
  if(row.TenantId!=tenant)throw new DomainException("Facility not found.",404);
  if(row.IsActive&&!input.IsActive){
   // The control plane cannot inspect tenant clinical tables. Fail closed when a deployed location reference needs a reviewed bounded check.
   var references=await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM pg_catalog.pg_constraint WHERE contype='f' AND confrelid='public.\"Facilities\"'::regclass",ignoreTenant:true);
   if(references>0)throw new DomainException("Facility reference review is required before deactivation.",409);
  }
  if(input.IsPrimary&&!input.IsActive)throw Invalid("The primary facility must be active.");
  if(row.IsPrimary&&!input.IsPrimary)throw Invalid("Select a different primary facility before changing this one.");
  var code=string.IsNullOrWhiteSpace(input.Code)?null:input.Code.Trim();
  if(code is not null){
   var duplicates=await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"Facilities\" WHERE \"TenantId\"=@tenant AND LOWER(BTRIM(\"Code\"))=LOWER(@Code) AND \"Id\"<>@Id AND NOT \"IsDeleted\"",new{tenant,Code=code,row.Id},true);
   if(duplicates>0)throw new DomainException("Facility code already exists for this tenant.",409);
  }
  var active=await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"Facilities\" WHERE \"TenantId\"=@tenant AND \"IsActive\" AND NOT \"IsDeleted\"",new{tenant},true);
  var primaries=await store.QueryAsync<Facility>("SELECT * FROM \"Facilities\" WHERE \"TenantId\"=@tenant AND \"IsPrimary\" AND NOT \"IsDeleted\" FOR UPDATE",new{tenant},true);
  if(!input.IsPrimary&&active>0&&!primaries.Any(x=>x.IsActive))throw new DomainException("Select an active primary facility before continuing.",409);
  if(primaries.Count>1)throw new DomainException("Existing primary facility associations require review before this change.",409);
  row.Name=input.Name.Trim();row.Code=code;row.Address=input.Address;row.City=input.City;row.Country=input.Country;
  row.IsActive=input.IsActive;row.IsPrimary=input.IsPrimary||(active==0&&input.IsActive);
  if(row.IsPrimary){
   await store.ExecuteAsync("UPDATE \"Facilities\" SET \"IsPrimary\"=false,\"UpdatedAt\"=NOW() WHERE \"TenantId\"=@tenant AND \"Id\"<>@Id AND \"IsPrimary\" AND NOT \"IsDeleted\"",new{tenant,row.Id},true);
   if(!primaries.Any(x=>x.Id==row.Id))await store.InsertAsync(new PlatformAuditLog{PlatformUserId=actor,TenantId=tenant,Action="facility.primary.change",MetadataJson=Encode(new{reason=input.Reason,previousPrimaryIds=primaries.Select(x=>x.Id).ToArray(),primaryId=row.Id})},true);
  }
  await Save(store,row,!id.HasValue);return(before,row);
 });
 public async Task<object> Entitlements(Guid id) => await PlatformModuleAccess.ResolveAsync(db,id);
 public Task<object> Entitlement(Guid tenant,Guid module,EntitlementInput input,string key)=>Change("entitlements.write","entitlement.save",tenant,key,new{module,input},input.Reason,async(store,_)=>{
  var capability=await Find<PlatformModule>(store,module); if(!SupportedModules.All.Any(s=>s.Key==capability.Key))throw Invalid("Only supported application modules can be assigned.");
  if(input.ExpiresAt.HasValue&&input.ExpiresAt<=DateTime.UtcNow)throw Invalid("Expiry must be in the future.");
  if(input.IsEnabled){
   var current=await PlatformModuleAccess.ResolveAsync(store,tenant);
   if(SupportedModules.Dependencies(capability.Key).Any(dependency=>!current.Any(x=>x.Key==dependency&&x.Effective)))
    throw new DomainException("Enable the required module dependencies before enabling this module.",409);
  }
  var row=await store.QueryFirstOrDefaultAsync<TenantEntitlement>("SELECT * FROM \"TenantEntitlements\" WHERE \"TenantId\"=@tenant AND \"PlatformModuleId\"=@module",new{tenant,module},true);
  var before=row is null?null:Encode(row);var create=row is null;
  // Resetting to plan policy soft-deletes the override. Restore it explicitly:
  // the shared UpdateAsync intentionally excludes soft-deleted rows.
  if(row is { IsDeleted:true }) {
   var restored=await store.ExecuteAsync("UPDATE \"TenantEntitlements\" SET \"IsDeleted\"=false WHERE \"Id\"=@Id AND \"TenantId\"=@tenant AND \"PlatformModuleId\"=@module AND \"IsDeleted\"",new{row.Id,tenant,module},true);
   if(restored!=1)throw new DomainException("The module override changed. Reload and try again.",409);
  }
  row??=new TenantEntitlement{TenantId=tenant,PlatformModuleId=module};
  row.IsEnabled=input.IsEnabled;row.Limit=input.Limit;row.ExpiresAt=input.ExpiresAt?.ToUniversalTime();row.Version++;row.IsDeleted=false;
  if(create)await store.InsertAsync(row,true);else await store.UpdateAsync(row,true);
  return(before,row);
 });
 public async Task<object> Audit(string? search,Guid? tenantId,int page) => await db.QueryAsync<AuditRow>(
  "SELECT a.\"Id\",a.\"Action\",a.\"CreatedAt\",a.\"MetadataJson\",a.\"TenantId\",u.\"Name\" AS \"Actor\",t.\"Name\" AS \"Tenant\" FROM \"PlatformAuditLogs\" a LEFT JOIN \"PlatformUsers\" u ON u.\"Id\"=a.\"PlatformUserId\" LEFT JOIN \"Tenants\" t ON t.\"Id\"=a.\"TenantId\" WHERE (@tenantId IS NULL OR a.\"TenantId\"=@tenantId) AND (@search='' OR a.\"Action\" ILIKE @pattern OR t.\"Name\" ILIKE @pattern OR u.\"Name\" ILIKE @pattern) ORDER BY a.\"CreatedAt\" DESC,a.\"Id\" LIMIT 50 OFFSET @offset",
  new{tenantId,search=search??"",pattern="%"+search+"%",offset=(Math.Clamp(page,1,100000)-1)*50},true);
 public sealed class AuditRow { public Guid Id{get;set;} public string Action{get;set;}=""; public string? Actor{get;set;} public string? Tenant{get;set;} public Guid? TenantId{get;set;} public string? MetadataJson{get;set;} public DateTime CreatedAt{get;set;} }
 public async Task<object> SupportSessions() => await db.QueryAsync<SupportRow>(
  "SELECT s.*,t.\"Name\" AS \"TenantName\",u.\"Name\" AS \"OperatorName\" FROM \"PlatformSupportSessions\" s JOIN \"Tenants\" t ON t.\"Id\"=s.\"TenantId\" JOIN \"PlatformUsers\" u ON u.\"Id\"=s.\"PlatformUserId\" WHERE NOT s.\"IsDeleted\" ORDER BY s.\"CreatedAt\" DESC LIMIT 200",ignoreTenant:true);
 public sealed class SupportRow:PlatformSupportSession { public string TenantName{get;set;}=""; public string OperatorName{get;set;}=""; }
 public Task<object> Support(SupportInput input,string key)=>Change("support.write","support.create",input.TenantId,key,input,input.Reason,async(store,actor)=>{
  var row=new PlatformSupportSession{PlatformUserId=actor,TenantId=input.TenantId,Reason=input.Reason,Scope="ReadOnly",ExpiresAt=DateTime.UtcNow.AddMinutes(input.DurationMinutes)};
  await store.InsertAsync(row,true);return(null,row);
 });
 public Task<object> Revoke(Guid id,PlatformChange input,string key)=>Change("support.write","support.revoke",null,key,new{id,input},input.Reason,async(store,actor)=>{
  var row=await Find<PlatformSupportSession>(store,id);var before=Encode(row);row.RevokedAt??=DateTime.UtcNow;await store.UpdateAsync(row,true);return(before,row);
 });
 public async Task<object> SupportSnapshot(Guid id) {
  var (actor,_)=await access.RequireAsync("support.read");
  var row=await Find<PlatformSupportSession>(db,id);
  if(row.PlatformUserId!=actor.Id||row.RevokedAt.HasValue||row.ExpiresAt<=DateTime.UtcNow)throw new DomainException("Support session is expired, revoked, or belongs to another operator.",403);
  var tenant=await Find<Tenant>(db,row.TenantId);
  var usage=await Usage(db,row.TenantId);
  await db.InsertAsync(new PlatformAuditLog{PlatformUserId=actor.Id,TenantId=row.TenantId,Action="support.read",MetadataJson=Encode(new{sessionId=id,reason=row.Reason})},true);
  return new { Tenant=TenantView(tenant),Usage=usage };
 }
 public async Task<object> Jobs()=>await db.QueryAsync<PlatformOperationJob>("SELECT * FROM \"PlatformOperationJobs\" WHERE NOT \"IsDeleted\" ORDER BY \"CreatedAt\" DESC LIMIT 200",ignoreTenant:true);
 public Task<object> Provision(JobInput input,string key)=>Change("operations.write","tenant.provision",input.TenantId,key,input,input.Reason,async(store,_)=>{
  var tenant=await Find<Tenant>(store,input.TenantId);
  if(tenant.LifecycleStatus!=CureFlow.Domain.Enums.TenantLifecycleStatus.Active)throw Invalid("Approve the tenant before provisioning.");
  var job=new PlatformOperationJob{TenantId=tenant.Id,JobType="ProvisionDefaults",Status="Running",StartedAt=DateTime.UtcNow};
  await store.InsertAsync(job,true);
  var count=await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"Facilities\" WHERE \"TenantId\"=@Id AND NOT \"IsDeleted\"",new{tenant.Id},true);
  if(count==0)await store.InsertAsync(new Facility{TenantId=tenant.Id,Name=tenant.Name,Code="PRIMARY",IsPrimary=true,Country=tenant.Country},true);
  // Module access inherits the current plan unless explicitly overridden.
  job.Status="Completed";job.CompletedAt=DateTime.UtcNow;await store.UpdateAsync(job,true);return(null,job);
 });
}



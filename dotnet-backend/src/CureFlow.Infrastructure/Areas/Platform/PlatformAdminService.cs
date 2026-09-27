using CureFlow.Application.Areas.Platform;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Common;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace CureFlow.Infrastructure.Areas.Platform;

public sealed partial class PlatformAdminService(ICureFlowDbSession db, PlatformAccess access, IPasswordHasher hasher)
{
 private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
 private static object Snapshot(object value) => value is PlatformUser u ? new { u.Id, u.Name, u.Email, u.Role, u.IsActive } : value;
 private static string Encode(object? value) => JsonSerializer.Serialize(value, Json);
 private static DomainException Invalid(string message) => new(message, 400);
 private static void Text(string? value, string label) { if (string.IsNullOrWhiteSpace(value)) throw Invalid(label + " is required."); }
 private static async Task<T> Find<T>(ICureFlowDbSession store, Guid id) where T : BaseEntity, new()
 {
  var row = await store.GetByIdAsync<T>(id, true);
  return row is null || row.IsDeleted ? throw new DomainException("The requested record was not found.", 404) : row;
 }
 private static async Task Save(ICureFlowDbSession store, BaseEntity value, bool create)
 {
  // Keep the concrete model type so the shared DAL uses its mapped table.
  switch (value) {
   case Facility x: if(create) await store.InsertAsync(x,true); else await store.UpdateAsync(x,true); break;
   case PlatformModule x: if(create) await store.InsertAsync(x,true); else await store.UpdateAsync(x,true); break;
   case PlatformUser x: if(create) await store.InsertAsync(x,true); else await store.UpdateAsync(x,true); break;
   case PlatformRole x: if(create) await store.InsertAsync(x,true); else await store.UpdateAsync(x,true); break;
  }
 }
 private async Task<object> Change(string permission, string action, Guid? tenantId, string requestId, object input, string reason,
  Func<ICureFlowDbSession, Guid, Task<(object? Before, object After)>> apply)
 {
  Text(reason,"Reason");
  if (!Guid.TryParse(requestId, out _)) throw Invalid("A valid X-Request-ID is required.");
  object result = new {};
  var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(action + tenantId + Encode(input))));
  await db.TransactionAsync(async store => {
   // Serialize infrequent control-plane mutations, including last-owner and primary-facility invariants.
   await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)", ignoreTenant: true);
   var (actor, _) = await access.RequireAsync(permission, store);
   var prior = await store.QueryFirstOrDefaultAsync<PlatformCommand>("SELECT * FROM \"PlatformCommands\" WHERE \"ActorId\" = @Id AND \"RequestId\" = @requestId", new { actor.Id, requestId }, true);
   if (prior is not null) {
    if (prior.Fingerprint != fingerprint) throw new DomainException("Request ID was already used for a different change.",409);
    result = JsonSerializer.Deserialize<JsonElement>(prior.ResponseJson); return;
   }
   if (tenantId.HasValue) await Find<Tenant>(store,tenantId.Value);
   var (before, after) = await apply(store, actor.Id);
   result = after;
   await store.InsertAsync(new PlatformAuditLog { PlatformUserId=actor.Id, TenantId=tenantId, Action=action,
    MetadataJson=Encode(new { reason, requestId, before, after=Snapshot(after) }) }, true);
   await store.InsertAsync(new PlatformCommand { ActorId=actor.Id, RequestId=requestId, Fingerprint=fingerprint, ResponseJson=Encode(after) },true);
  });
  return result;
 }
 public async Task<object> Me() { var (u,p)=await access.RequireAsync(); return new { u.Id,u.Name,u.Email,u.Role,Permissions=p }; }
 public async Task<object> Roles() => (await db.GetAllAsync<PlatformRole>(ignoreTenant:true)).Where(x=>!x.IsDeleted).Select(x=>new {x.Id,x.Key,x.Name,Permissions=JsonSerializer.Deserialize<string[]>(x.PermissionsJson)});
 public async Task<object> Operators() => (await db.GetAllAsync<PlatformUser>(ignoreTenant:true)).Where(x=>!x.IsDeleted).Select(Snapshot);
 public async Task<object> Tenants() => (await db.GetAllAsync<Tenant>(ignoreTenant:true)).Where(x=>!x.IsDeleted).Select(TenantView);
 private static object TenantView(Tenant x) => new {x.Id,x.Name,x.Slug,x.TenantType,x.ContactName,x.ContactEmail,x.ContactPhone,x.Country,x.Timezone,x.IsActive,x.LifecycleStatus,x.RejectionReason,x.Plan,x.SubscriptionStatus,x.SeatLimit,x.PatientLimit,x.MessagesQuotaMonthly,x.TrialEndsAt,x.SubscriptionRenewsAt,x.OnboardingComplete,x.CreatedAt};
 public Task<object> Profile(Guid id,TenantProfileInput input,string key)=>Change("tenants.write","tenant.profile",id,key,input,input.Reason,async(store,_)=>{
  Text(input.Name,"Name");
  try { TimeZoneInfo.FindSystemTimeZoneById(input.Timezone); }
  catch (TimeZoneNotFoundException) { throw Invalid("Select a valid timezone."); }
  var tenant=await Find<Tenant>(store,id);var before=TenantView(tenant);
  tenant.Name=input.Name.Trim();tenant.ContactName=input.ContactName?.Trim();tenant.ContactEmail=input.ContactEmail?.Trim();
  tenant.ContactPhone=input.ContactPhone?.Trim();tenant.Country=input.Country?.ToUpperInvariant();tenant.Timezone=input.Timezone;
  await store.UpdateAsync(tenant,true);return(before,TenantView(tenant));
 });
 public async Task<object> TenantDetail(Guid id) {
  var t=await Find<Tenant>(db,id);
  return new { Tenant=TenantView(t),Usage=await Usage(db,id) };
 }
 private static Task<UsageCounts> Usage(ICureFlowDbSession store,Guid id) => store.QuerySingleAsync<UsageCounts>(
  "SELECT (SELECT COUNT(*) FROM \"Users\" WHERE \"TenantId\"=@id AND NOT \"IsDeleted\") AS \"Users\", (SELECT COUNT(*) FROM \"Patients\" WHERE \"TenantId\"=@id AND NOT \"IsDeleted\") AS \"Patients\"",new{id},true);
 public sealed class UsageCounts { public long Users {get;set;} public long Patients {get;set;} }
 public async Task<object> Overview() {
  var tenants=(await db.GetAllAsync<Tenant>(ignoreTenant:true)).Where(x=>!x.IsDeleted).ToArray();
  return new { Total=tenants.Length, Active=tenants.Count(x=>x.LifecycleStatus==TenantLifecycleStatus.Active), Pending=tenants.Count(x=>x.LifecycleStatus==TenantLifecycleStatus.PendingApproval),
   Suspended=tenants.Count(x=>x.LifecycleStatus==TenantLifecycleStatus.Suspended), PastDue=tenants.Count(x=>x.SubscriptionStatus==SubscriptionStatus.PastDue),
   ByType=tenants.GroupBy(x=>x.TenantType).Select(x=>new{Type=x.Key,Count=x.Count()}), Database="Connected", CheckedAt=DateTime.UtcNow };
 }
 public Task<object> Lifecycle(Guid id,string action,PlatformChange input,string key) => Change("tenants.write","tenant."+action,id,key,input,input.Reason,async(store,actor)=>{
  var t=await Find<Tenant>(store,id); var before=TenantView(t);
  if(!PlatformPermissions.CanTransition(t.LifecycleStatus,action)) throw new DomainException("This lifecycle action is not allowed in the tenant's current state.",409);
  t.LifecycleStatus=action switch { "approve" or "activate"=>TenantLifecycleStatus.Active,"suspend"=>TenantLifecycleStatus.Suspended,"reject"=>TenantLifecycleStatus.Rejected,_=>TenantLifecycleStatus.PendingApproval};
  t.IsActive=t.LifecycleStatus==TenantLifecycleStatus.Active;
  if(action=="approve"){t.ApprovedAt=DateTime.UtcNow;t.ApprovedByPlatformUserId=actor;}
  t.RejectionReason=action=="reject"?input.Reason:null;
  await store.UpdateAsync(t,true);return(before,TenantView(t));
 });
 public Task<object> Type(Guid id,TenantTypeInput input,string key)=>Change("tenants.write","tenant.type",id,key,input,input.Reason,async(store,_)=>{
  var t=await Find<Tenant>(store,id);var before=TenantView(t);t.TenantType=input.TenantType;await store.UpdateAsync(t,true);return(before,TenantView(t));
 });
 public Task<object> Plan(Guid id,SubscriptionInput input,string key)=>Change("billing.write","subscription.update",id,key,input,input.Reason,async(store,_)=>{
  var t=await Find<Tenant>(store,id);var before=TenantView(t);
  var usage=await Usage(store,id);
  if(input.SeatLimit<usage.Users||input.PatientLimit<usage.Patients) throw Invalid("Limits cannot be lower than current usage.");
  t.Plan=input.Plan;t.SubscriptionStatus=input.Status;t.SeatLimit=input.SeatLimit;t.PatientLimit=input.PatientLimit;t.MessagesQuotaMonthly=input.MessagesQuotaMonthly;
  t.TrialEndsAt=input.TrialEndsAt?.ToUniversalTime();t.SubscriptionRenewsAt=input.SubscriptionRenewsAt?.ToUniversalTime();
  await store.UpdateAsync(t,true);return(before,TenantView(t));
 });
 public Task<object> Operator(Guid? id,OperatorInput input,string key)=>Change("operators.write","operator.save",null,key,new{id,input},input.Reason,async(store,actor)=>{
  Text(input.Name,"Name"); Text(input.Email,"Email");
  var user=id.HasValue?await Find<PlatformUser>(store,id.Value):new PlatformUser();
  var before=id.HasValue?Snapshot(user):null;
  var actorUser=await Find<PlatformUser>(store,actor);
  if((user.Role=="owner"&&id.HasValue||input.Role=="owner") && actorUser.Role!="owner")throw new DomainException("Only an owner can manage owners.",403);
  var role=await store.QueryFirstOrDefaultAsync<PlatformRole>("SELECT * FROM \"PlatformRoles\" WHERE \"Key\"=@Role AND NOT \"IsDeleted\"",new{input.Role},true);
  if(role is null)throw Invalid("Select an existing platform role.");
  if(id.HasValue&&id.Value==actor&&(!input.IsActive||input.Role!=user.Role))throw Invalid("You cannot deactivate yourself or change your own role.");
  if(user.Role=="owner"&&id.HasValue&&(!input.IsActive||input.Role!="owner")){
   var owners=await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"PlatformUsers\" WHERE \"Role\"='owner' AND \"IsActive\" AND NOT \"IsDeleted\"",ignoreTenant:true);
   if(owners<=1)throw Invalid("At least one active owner is required.");
  }
  var email=input.Email.Trim().ToLowerInvariant();
  var duplicate=await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"PlatformUsers\" WHERE LOWER(\"Email\")=@email AND \"Id\"<>@Id",new{email,user.Id},true);
  if(duplicate>0)throw new DomainException("Email is already registered.",409);
  if(!id.HasValue&&string.IsNullOrEmpty(input.Password))throw Invalid("Password is required for a new operator.");
  user.Name=input.Name.Trim();user.Email=email;user.Role=input.Role;user.IsActive=input.IsActive;
  if(!string.IsNullOrEmpty(input.Password))user.PasswordHash=hasher.Hash(input.Password);
  await Save(store,user,!id.HasValue);return(before,Snapshot(user));
 });
 public Task<object> Role(Guid? id,RoleInput input,string key)=>Change("roles.write","role.save",null,key,new{id,input},input.Reason,async(store,actor)=>{
  var actorUser=await Find<PlatformUser>(store,actor);
  if(actorUser.Role!="owner")throw new DomainException("Only owners can change permission groups.",403);
  var row=id.HasValue?await Find<PlatformRole>(store,id.Value):new PlatformRole();
  var before=id.HasValue?Encode(row):null;
  if(row.Key=="owner"||input.Key=="owner")throw Invalid("The owner permission group is protected.");
  if(input.Permissions.Any(p=>!PlatformPermissions.All.Contains(p)))throw Invalid("Unknown platform permission.");
  if(id.HasValue&&input.Key!=row.Key)throw Invalid("Role keys cannot be renamed.");
  var duplicate=await store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"PlatformRoles\" WHERE \"Key\"=@Key AND \"Id\"<>@Id",new{input.Key,row.Id},true);
  if(duplicate>0)throw new DomainException("Role key already exists.",409);
  Text(input.Name,"Name");row.Key=input.Key;row.Name=input.Name.Trim();row.PermissionsJson=Encode(input.Permissions.Distinct().ToArray());
  await Save(store,row,!id.HasValue);return(before,new{row.Id,row.Key,row.Name,Permissions=input.Permissions});
 });
}

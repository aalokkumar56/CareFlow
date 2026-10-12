using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class AppointmentWaitlistService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
 IIdempotentCommandExecutor commands,IAppointmentBookingService bookings,IClinicalAccessAuditService audit,IOutboxWriter outbox,IConfiguration config):IAppointmentWaitlistService
{
 private (string Version,TimeSpan Retry,int Days,int MaximumActive) Policy()
 {
  var version=config["AppointmentWaitlists:ApprovedPolicyVersion"];var hours=config.GetValue<int?>("AppointmentWaitlists:ApprovedRetryHorizonHours");
  var days=config.GetValue<int?>("AppointmentWaitlists:ApprovedMaximumWaitDays");
  var active=config.GetValue<int?>("AppointmentWaitlists:ApprovedMaximumActiveEntriesPerPatient");
  if(string.IsNullOrWhiteSpace(version)||version.Length>100||version.Any(char.IsControl)||hours is not >0 or >8760||days is not >0 or >365||active is not >0 or >50)
   throw new DomainException("The reviewed waitlist expiry/retry policy is not configured.",503);
  return(version,TimeSpan.FromHours(hours.Value),days.Value,active.Value);
 }
 private static void Reason(string reason)
 {
  if(string.IsNullOrWhiteSpace(reason)||reason.Trim().Length is <3 or >1000||reason.Any(char.IsControl))throw new ValidationException("Supply a bounded accountable reason.");
 }
 private async Task RequireAsync(ICureFlowDbSession store,string capability,Guid patientId,CancellationToken ct)
 {
  if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)throw new ForbiddenException();
  var user=await store.QueryFirstOrDefaultAsync<User>("""
   SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
   """,new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
  var codes=await permissions.GetPermissionCodesAsync(user,ct,store);
  if(!codes.Contains(capability,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal)
    ||!codes.Contains(CureFlowPermissions.StaffView,StringComparer.Ordinal))throw new ForbiddenException();
  if(await store.GetByIdAsync<Patient>(patientId,ct:ct) is not {IsActive:true,IsDeleted:false})throw new NotFoundException("Patient");
 }
 private static Guid[] Resources(AppointmentWaitlistEntry entry)
  =>JsonSerializer.Deserialize<Guid[]>(entry.ResourceIdsJson)??throw new InvalidOperationException("Waitlist target evidence is missing.");
 private static async Task<AppointmentWaitlistEntry> EntryAsync(ICureFlowDbSession store,Guid id,bool locked,CancellationToken ct)
  =>await store.QueryFirstOrDefaultAsync<AppointmentWaitlistEntry>($"""
   SELECT * FROM "AppointmentWaitlistEntries" WHERE "TenantId"=@TenantId AND "Id"=@id {(locked?"FOR UPDATE":"")}
   """,new{id},ct:ct)??throw new NotFoundException("Waitlist entry");
 private async Task AuthorizeAsync(ICureFlowDbSession store,Guid id,string capability,CancellationToken ct)
 {
  var entry=await EntryAsync(store,id,false,ct);await RequireAsync(store,capability,entry.PatientId,ct);
 }
 private async Task DecisionAsync(ICureFlowDbSession store,AppointmentWaitlistEntry entry,string action,Guid? offerId,string reason,CancellationToken ct)
 {
  await store.InsertAsync(new AppointmentWaitlistDecision{EntryId=entry.Id,PatientId=entry.PatientId,EntryRevision=entry.Revision,
   OfferId=offerId,Action=action,Reason=reason.Trim()},ct:ct);
  await audit.RecordAsync(new(entry.PatientId,"AppointmentWaitlistEntry",entry.Id,action,"committed",entry.Revision.ToString(),"waitlist"),ct,store);
 }
 private static async Task SaveEntryAsync(ICureFlowDbSession store,AppointmentWaitlistEntry entry,CancellationToken ct)
  =>await store.ExecuteAsync("""
   UPDATE "AppointmentWaitlistEntries" SET "State"=@state,"Revision"=@revision,"CurrentOfferId"=@offerId,"UpdatedAt"=CURRENT_TIMESTAMP
    WHERE "TenantId"=@TenantId AND "Id"=@id
   """,new{id=entry.Id,state=entry.State,revision=entry.Revision,offerId=entry.CurrentOfferId},ct:ct);
 private static async Task ScopeAsync(ICureFlowDbSession store,AppointmentWaitlistEntry entry,CancellationToken ct)
 {
  if(!(await store.QueryFirstOrDefaultAsync<Guid?>("SELECT public.cureflow_lock_active_facility(@TenantId,@id)",new{id=entry.FacilityId},ct:ct)).HasValue)
   throw new NotFoundException("Active facility");
  if(!await store.QueryFirstOrDefaultAsync<bool>("""
   SELECT EXISTS(SELECT 1 FROM "Users" u JOIN "StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
    WHERE u."TenantId"=@TenantId AND u."Id"=@id AND u."Role"=ANY(@roles) AND u."IsActive" AND NOT u."IsDeleted" AND p."IsActive" AND NOT p."IsDeleted")
   """,new{id=entry.PractitionerUserId,roles=new[]{(int)CureFlow.Domain.Enums.UserRole.Doctor,(int)CureFlow.Domain.Enums.UserRole.Nurse,(int)CureFlow.Domain.Enums.UserRole.Staff}},ct:ct))throw new NotFoundException("Current staff");
  if(!await store.QueryFirstOrDefaultAsync<bool>("""
   SELECT EXISTS(SELECT 1 FROM "PatientIdentifiers" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId AND "FacilityId"=@facilityId AND "Namespace"='local-mrn')
   """,new{entry.PatientId,entry.FacilityId},ct:ct))throw new ConflictException("Waitlist requires the patient's explicit facility-local identifier.");
  foreach(var id in Resources(entry))
   if(!await store.QueryFirstOrDefaultAsync<bool>("""
    SELECT EXISTS(SELECT 1 FROM "FacilityResources" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@id
      AND "ResourceType" IN('room','equipment') AND "OperationalStatus"='enabled' AND "IsActive" AND NOT "IsDeleted")
    """,new{entry.FacilityId,id},ct:ct))throw new NotFoundException("Current facility resource");
 }
 public Task<IdempotentCommandResult> AddAsync(AppointmentWaitlistInput input,string key,CancellationToken ct)
 {
  var policy=Policy();Reason(input.Reason);
  if(input.PatientId==Guid.Empty||input.FacilityId==Guid.Empty||input.PractitionerUserId==Guid.Empty||input.ResourceIds is null
   ||input.ResourceIds.Count>5||input.ResourceIds.Any(id=>id==Guid.Empty)||input.ResourceIds.Distinct().Count()!=input.ResourceIds.Count
   ||input.EarliestStartsAtUtc.Kind!=DateTimeKind.Utc||input.LatestEndsAtUtc.Kind!=DateTimeKind.Utc
   ||input.EarliestStartsAtUtc<=DateTime.UtcNow||input.LatestEndsAtUtc> DateTime.UtcNow.AddDays(policy.Days)
   ||input.LatestEndsAtUtc-input.EarliestStartsAtUtc<TimeSpan.FromMinutes(input.DurationMinutes)||input.DurationMinutes is <1 or >480
   ||input.BookingKind is not ("routine" or "walk_in" or "follow_up")||(input.BookingKind=="follow_up")!=input.FollowUpAppointmentId.HasValue
   ||input.FollowUpAppointmentId==Guid.Empty)throw new ValidationException("Supply explicit patient/facility/targets, bounded future UTC window and booking kind.");
  var ids=input.ResourceIds.OrderBy(id=>id.ToString("N"),StringComparer.Ordinal).ToArray();
  return commands.ExecuteAsync("appointment.waitlist.add",key,new{input.PatientId,input.FacilityId,input.PractitionerUserId,ResourceIds=ids,
    input.EarliestStartsAtUtc,input.LatestEndsAtUtc,input.DurationMinutes,input.BookingKind,input.FollowUpAppointmentId,input.Reason},policy.Retry,
   (store,token)=>RequireAsync(store,CureFlowPermissions.AppointmentCreate,input.PatientId,token),async(store,token)=>{
    await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
    await RequireAsync(store,CureFlowPermissions.AppointmentCreate,input.PatientId,token);
    if(await store.QueryFirstOrDefaultAsync<int>("""
     SELECT count(*)::integer FROM "AppointmentWaitlistEntries" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId
      AND "State" IN('waiting','offered') AND "ExpiresAtUtc">clock_timestamp()
     """,new{input.PatientId},ct:token)>=policy.MaximumActive)throw new ConflictException("The patient has reached the approved active waitlist limit.");
    var entry=new AppointmentWaitlistEntry{PatientId=input.PatientId,FacilityId=input.FacilityId,PractitionerUserId=input.PractitionerUserId,
     ResourceIdsJson=JsonSerializer.Serialize(ids),EarliestStartsAtUtc=input.EarliestStartsAtUtc,LatestEndsAtUtc=input.LatestEndsAtUtc,
     DurationMinutes=input.DurationMinutes,ExpiresAtUtc=input.LatestEndsAtUtc,BookingKind=input.BookingKind,
     FollowUpAppointmentId=input.FollowUpAppointmentId,PolicyVersion=policy.Version};
    await ScopeAsync(store,entry,token);
    if(input.FollowUpAppointmentId.HasValue)
    {
     var prior=await store.GetByIdAsync<Appointment>(input.FollowUpAppointmentId.Value,ct:token)??throw new NotFoundException("Follow-up source");
     if(prior.PatientId!=entry.PatientId||prior.ScheduledAt>=entry.EarliestStartsAtUtc||prior.Status is CureFlow.Domain.Enums.AppointmentStatus.Cancelled or CureFlow.Domain.Enums.AppointmentStatus.NoShow)
      throw new ConflictException("Waitlist follow-up requires an earlier same-patient source.");
    }
    await store.InsertAsync(entry,ct:token);await DecisionAsync(store,entry,"add",null,input.Reason,token);
    return(new CommandReceipt(entry.Id,entry.Revision,CommandOutcome.Created),201);
   },ct);
 }
 public async Task<AppointmentWaitlistView> GetAsync(Guid id,CancellationToken ct)
 {
  AppointmentWaitlistView? result=null;
  await db.TransactionAsync(async store=>{
   var entry=await EntryAsync(store,id,false,ct);await RequireAsync(store,CureFlowPermissions.AppointmentView,entry.PatientId,ct);
   AppointmentWaitlistOfferView? offer=null;
   if(entry.CurrentOfferId.HasValue)
   {
    var row=await store.GetByIdAsync<AppointmentWaitlistOffer>(entry.CurrentOfferId.Value,ct:ct)??throw new NotFoundException("Offer");
    offer=new(row.Id,row.ReservationId,row.ExpiresAtUtc,row.State=="offered"&&row.ExpiresAtUtc<=DateTime.UtcNow?"expired":row.State,row.Revision);
   }
   var state=entry.State is "waiting" or "offered"&&entry.ExpiresAtUtc<=DateTime.UtcNow?"expired":entry.State;
   result=new(entry.Id,entry.PatientId,entry.FacilityId,entry.PractitionerUserId,Resources(entry),entry.EarliestStartsAtUtc,entry.LatestEndsAtUtc,
    entry.DurationMinutes,entry.BookingKind,entry.FollowUpAppointmentId,entry.ExpiresAtUtc,state,entry.Revision,offer);
   await audit.RecordAsync(new(entry.PatientId,"AppointmentWaitlistEntry",id,"read","authorized",entry.Revision.ToString(),"waitlist"),ct,store);
  },ct);return result!;
 }
 private async Task ExpireOfferAsync(ICureFlowDbSession store,AppointmentWaitlistEntry entry,string reason,CancellationToken ct)
 {
  if(entry.State!="offered"||entry.CurrentOfferId is null)return;
  var offer=await store.GetByIdAsync<AppointmentWaitlistOffer>(entry.CurrentOfferId.Value,ct:ct)??throw new NotFoundException("Offer");
  if(offer.State!="offered"||offer.ExpiresAtUtc>DateTime.UtcNow)throw new ConflictException("The current offer is still live.");
  await CloseHoldAsync(store,offer,ct);
  await store.ExecuteAsync("""
   UPDATE "AppointmentWaitlistOffers" SET "State"='expired',"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "Id"=@id
   """,new{id=offer.Id},ct:ct);
  entry.State="waiting";entry.CurrentOfferId=null;entry.Revision++;await SaveEntryAsync(store,entry,ct);
  await DecisionAsync(store,entry,"expire_offer",offer.Id,reason,ct);
 }
 private async Task CloseHoldAsync(ICureFlowDbSession store,AppointmentWaitlistOffer offer,CancellationToken ct)
 {
  var hold=await store.GetByIdAsync<AppointmentReservation>(offer.ReservationId,ct:ct)??throw new NotFoundException("Offered hold");
  if(hold.State is "expired" or "released")return;
  if(hold.State!="held"||hold.OwnerUserId!=tenant.UserId&&hold.HoldExpiresAtUtc>DateTime.UtcNow)throw new ConflictException("Offer owner must release a live hold; confirmed capacity cannot be withdrawn here.");
  var state=hold.HoldExpiresAtUtc<=DateTime.UtcNow?"expired":"released";
  await store.ExecuteAsync("""
   UPDATE "AppointmentReservations" SET "State"=@state,"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "Id"=@id
   """,new{id=hold.Id,state},ct:ct);
  await store.ExecuteAsync("""
   UPDATE "AppointmentReservationSeats" SET "State"=@state,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "ReservationId"=@id
   """,new{id=hold.Id,state},ct:ct);
 }
 public Task<IdempotentCommandResult> OfferAsync(Guid id,AppointmentWaitlistOfferInput input,string key,CancellationToken ct)
 {
  Reason(input.Reason);if(input.ExpectedRevision<1||input.ExpectedReservationRevision<1||input.ReservationId==Guid.Empty||input.ReleasedReservationId==Guid.Empty)
   throw new ValidationException("Supply current entry/hold versions and explicit released capacity.");
  var policy=Policy();
  return commands.ExecuteAsync("appointment.waitlist.offer",key,new{id,input},policy.Retry,
   (store,token)=>AuthorizeAsync(store,id,CureFlowPermissions.AppointmentCreate,token),async(store,token)=>{
    await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
    var entry=await EntryAsync(store,id,true,token);await RequireAsync(store,CureFlowPermissions.AppointmentCreate,entry.PatientId,token);
    if(entry.Revision!=input.ExpectedRevision)throw new DomainException("Waitlist entry changed; reload.",412);
    if(entry.ExpiresAtUtc<=DateTime.UtcNow||entry.LatestEndsAtUtc>DateTime.UtcNow.AddDays(policy.Days))throw new ConflictException("Waitlist entry expired or exceeds the current approved horizon.");
    await ExpireOfferAsync(store,entry,"Previous offer expiry before replacement offer.",token);
    if(entry.State!="waiting")throw new ConflictException("Only a waiting entry can receive an offer.");
    await ScopeAsync(store,entry,token);
    var source=await store.GetByIdAsync<AppointmentReservation>(input.ReleasedReservationId,ct:token)??throw new NotFoundException("Released capacity");
    var hold=await store.GetByIdAsync<AppointmentReservation>(input.ReservationId,ct:token)??throw new NotFoundException("Owned replacement hold");
    if(source.State!="released"||source.FacilityId!=entry.FacilityId||source.PractitionerUserId!=entry.PractitionerUserId
     ||hold.State!="held"||hold.OwnerUserId!=tenant.UserId||hold.Revision!=input.ExpectedReservationRevision||hold.HoldExpiresAtUtc<=DateTime.UtcNow
     ||hold.FacilityId!=source.FacilityId||hold.PractitionerUserId!=source.PractitionerUserId||hold.StartsAtUtc!=source.StartsAtUtc||hold.EndsAtUtc!=source.EndsAtUtc
     ||hold.StartsAtUtc<entry.EarliestStartsAtUtc||hold.EndsAtUtc>entry.LatestEndsAtUtc||hold.StartsAtUtc<=DateTime.UtcNow
     ||(hold.EndsAtUtc-hold.StartsAtUtc).TotalMinutes!=entry.DurationMinutes)throw new ConflictException("Offer must bind exact released capacity to a current owned hold within this request.");
    foreach(var reservation in new[]{source.Id,hold.Id})
    {
     var resources=(await store.QueryAsync<Guid>("""
      SELECT "TargetId" FROM "AppointmentReservationSeats" WHERE "TenantId"=@TenantId AND "ReservationId"=@id AND "TargetType"='resource'
      """,new{id=reservation},ct:token)).OrderBy(g=>g.ToString("N"),StringComparer.Ordinal).ToArray();
     if(!resources.SequenceEqual(Resources(entry)))throw new ConflictException("Offer resource scope differs from requested/released capacity.");
    }
    if(source.AppointmentId.HasValue)
    {
     var sourceAppointment=await store.GetByIdAsync<Appointment>(source.AppointmentId.Value,ct:token)??throw new NotFoundException("Released source appointment");
     await audit.RecordAsync(new(sourceAppointment.PatientId,"AppointmentReservation",source.Id,"released_capacity","authorized",source.Revision.ToString(),"waitlist-offer"),token,store);
    }
    var offer=new AppointmentWaitlistOffer{EntryId=entry.Id,PatientId=entry.PatientId,ReleasedReservationId=source.Id,ReservationId=hold.Id,
     OwnerUserId=tenant.UserId!.Value,ExpiresAtUtc=hold.HoldExpiresAtUtc,PolicyVersion=policy.Version};
    await store.InsertAsync(offer,ct:token);entry.State="offered";entry.CurrentOfferId=offer.Id;entry.Revision++;
    await SaveEntryAsync(store,entry,token);await DecisionAsync(store,entry,"offer",offer.Id,input.Reason,token);
    await outbox.WriteAsync(store,Guid.NewGuid(),"appointment.waitlist-offered",1,"AppointmentWaitlistEntry",entry.Id,entry.Revision.ToString(),new{OfferId=offer.Id},token);
    return(new CommandReceipt(offer.Id,offer.Revision,CommandOutcome.Created),201);
   },ct);
 }
 public Task<IdempotentCommandResult> AcceptAsync(Guid id,Guid offerId,AppointmentWaitlistAcceptanceInput input,string key,CancellationToken ct)
 {
  Reason(input.Reason);if(input.ExpectedRevision<1||input.ExpectedOfferRevision<1||offerId==Guid.Empty)throw new ValidationException("Supply current entry/offer revisions.");
  var policy=Policy();
  return commands.ExecuteAsync("appointment.waitlist.accept",key,new{id,offerId,input},policy.Retry,
   (store,token)=>AuthorizeAsync(store,id,CureFlowPermissions.AppointmentCreate,token),async(store,token)=>{
    await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
    var entry=await EntryAsync(store,id,true,token);await RequireAsync(store,CureFlowPermissions.AppointmentCreate,entry.PatientId,token);
    if(entry.Revision!=input.ExpectedRevision)throw new DomainException("Waitlist entry changed; reload.",412);
    var offer=await store.GetByIdAsync<AppointmentWaitlistOffer>(offerId,ct:token)??throw new NotFoundException("Offer");
    if(entry.State!="offered"||entry.CurrentOfferId!=offerId||offer.EntryId!=id||offer.PatientId!=entry.PatientId||offer.OwnerUserId!=tenant.UserId
      ||offer.PolicyVersion!=policy.Version||offer.State!="offered"||offer.Revision!=input.ExpectedOfferRevision||offer.ExpiresAtUtc<=DateTime.UtcNow||entry.ExpiresAtUtc<=DateTime.UtcNow)
     throw new ConflictException("Offer changed, expired or is owned by another staff actor.");
    await ScopeAsync(store,entry,token);
    var hold=await store.GetByIdAsync<AppointmentReservation>(offer.ReservationId,ct:token)??throw new NotFoundException("Offered hold");
    var receipt=await bookings.BookInTransactionAsync(store,new(entry.PatientId,hold.Id,hold.Revision,entry.BookingKind,entry.FollowUpAppointmentId,null,null),token);
    await store.ExecuteAsync("""
     UPDATE "AppointmentWaitlistOffers" SET "State"='accepted',"AppointmentId"=@appointmentId,"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP
      WHERE "TenantId"=@TenantId AND "Id"=@offerId
     """,new{offerId,appointmentId=receipt.ResourceId},ct:token);
    entry.State="accepted";entry.Revision++;await SaveEntryAsync(store,entry,token);
    await DecisionAsync(store,entry,"accept",offerId,input.Reason,token);
    return(receipt,201);
   },ct);
 }
 public Task<IdempotentCommandResult> WithdrawAsync(Guid id,AppointmentWaitlistWithdrawalInput input,string key,CancellationToken ct)
 {
  Reason(input.Reason);if(input.ExpectedRevision<1)throw new ValidationException("Supply current entry revision.");
  var policy=Policy();
  return commands.ExecuteAsync("appointment.waitlist.withdraw",key,new{id,input},policy.Retry,
   (store,token)=>AuthorizeAsync(store,id,CureFlowPermissions.AppointmentEdit,token),async(store,token)=>{
    await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
    var entry=await EntryAsync(store,id,true,token);await RequireAsync(store,CureFlowPermissions.AppointmentEdit,entry.PatientId,token);
    if(entry.Revision!=input.ExpectedRevision)throw new DomainException("Waitlist entry changed; reload.",412);
    if(entry.State is not ("waiting" or "offered"))throw new ConflictException("Only waiting/offered entries may be withdrawn.");
    if(entry.CurrentOfferId.HasValue)
    {
     var offer=await store.GetByIdAsync<AppointmentWaitlistOffer>(entry.CurrentOfferId.Value,ct:token)??throw new NotFoundException("Offer");
     await CloseHoldAsync(store,offer,token);
     await store.ExecuteAsync("""
      UPDATE "AppointmentWaitlistOffers" SET "State"=@state,"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "Id"=@id
      """,new{id=offer.Id,state=offer.ExpiresAtUtc<=DateTime.UtcNow?"expired":"withdrawn"},ct:token);
    }
    entry.State=entry.ExpiresAtUtc<=DateTime.UtcNow?"expired":"withdrawn";entry.Revision++;await SaveEntryAsync(store,entry,token);
    await DecisionAsync(store,entry,entry.State,entry.CurrentOfferId,input.Reason,token);
    return(new CommandReceipt(entry.Id,entry.Revision,CommandOutcome.Updated),200);
   },ct);
 }
}
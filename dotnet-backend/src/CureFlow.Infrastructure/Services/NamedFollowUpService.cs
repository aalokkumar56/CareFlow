using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class NamedFollowUpService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
 IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IOutboxWriter outbox,IConfiguration config):INamedFollowUpService
{
 private TimeSpan Retry()
 {
  var hours=config.GetValue<int?>("NamedFollowUps:ApprovedRetryHorizonHours");
  if(hours is not >0 or >8760)throw new DomainException("The reviewed named follow-up retry policy is not configured.",503);
  return TimeSpan.FromHours(hours.Value);
 }
 private static void Reason(string value)
 {
  if(string.IsNullOrWhiteSpace(value)||value.Trim().Length is <3 or >1000||value.Any(char.IsControl))throw new ValidationException("Supply a bounded accountable reason.");
 }
 private static void Assignment(Guid? assignee,Guid owner,DateTime due)
 {
  if(assignee==Guid.Empty||owner==Guid.Empty||due.Kind!=DateTimeKind.Utc||due<=DateTime.UtcNow||due>DateTime.UtcNow.AddDays(365))
   throw new ValidationException("Supply explicit ownership and a future UTC due time within 365 days.");
 }
 private async Task RequireAsync(ICureFlowDbSession store,string capability,Guid patientId,CancellationToken ct)
 {
  if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null||store.TenantId!=tenant.TenantId)throw new ForbiddenException();
  var actor=await store.QueryFirstOrDefaultAsync<User>("SELECT * FROM \"Users\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@id AND \"IsActive\" AND NOT \"IsDeleted\" FOR SHARE",new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
  var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
  if(!codes.Contains(capability,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.FollowUpView,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal)
    ||!codes.Contains(CureFlowPermissions.ClinicalView,StringComparer.Ordinal))throw new ForbiddenException();
  if(!await store.QueryFirstOrDefaultAsync<bool>("SELECT public.cureflow_patient_read_allowed(@TenantId,@patientId)",new{patientId},ct:ct))throw new ForbiddenException();
  if(await store.GetByIdAsync<Patient>(patientId,ct:ct) is not {IsActive:true,IsDeleted:false})throw new NotFoundException("Patient");
 }
 private async Task OwnerAsync(ICureFlowDbSession store,Guid id,string capability,Guid patientId,CancellationToken ct)
 {
  var user=await store.QueryFirstOrDefaultAsync<User>("SELECT * FROM \"Users\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@id AND \"IsActive\" AND NOT \"IsDeleted\" FOR SHARE",new{id},ct:ct)??throw new NotFoundException("Current accountable user");
  var codes=await permissions.GetPermissionCodesAsync(user,ct,store);
  if(!codes.Contains(capability,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.FollowUpView,StringComparer.Ordinal)
    ||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.ClinicalView,StringComparer.Ordinal))
   throw new ConflictException("Accountable users require current named follow-up and clinical capabilities.");
  var restricted=await store.QueryFirstOrDefaultAsync<bool>("SELECT EXISTS(SELECT 1 FROM \"PatientConfidentialityStates\" WHERE \"TenantId\"=@TenantId AND \"PatientId\"=@patientId AND \"Restricted\")",new{patientId},ct:ct);
  if(restricted&&!codes.Contains(CureFlowPermissions.PatientRestrictedView,StringComparer.Ordinal))throw new ConflictException("Accountable user lacks current restricted-chart scope.");
 }
 private static async Task<ClinicalFollowUpWork> WorkAsync(ICureFlowDbSession store,Guid id,bool locked,CancellationToken ct)
  =>await store.QueryFirstOrDefaultAsync<ClinicalFollowUpWork>($"SELECT * FROM \"ClinicalFollowUpWorks\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@id {(locked?"FOR UPDATE":"")}",new{id},ct:ct)??throw new NotFoundException("Named follow-up");
 private async Task AuthorizeAsync(ICureFlowDbSession store,Guid id,string capability,CancellationToken ct)
 { var work=await WorkAsync(store,id,false,ct);await RequireAsync(store,capability,work.PatientId,ct); }
 private static NamedFollowUpView View(ClinicalFollowUpWork w)=>new(w.Id,w.PatientId,w.SourceType,w.SourceId,w.SourceVersion,w.AssignedUserId,
  w.EscalationOwnerUserId,w.DueAtUtc,w.State,w.Revision,w.AcknowledgedAtUtc,w.CompletedAtUtc,!w.AssignedUserId.HasValue,w.State!="completed"&&w.DueAtUtc<DateTime.UtcNow);
 private async Task EvidenceAsync(ICureFlowDbSession store,ClinicalFollowUpWork w,string action,string reason,Guid? noteId,int? noteRevision,CancellationToken ct)
 {
  await store.InsertAsync(new ClinicalFollowUpVersion{WorkId=w.Id,PatientId=w.PatientId,Revision=w.Revision,AssignedUserId=w.AssignedUserId,
   EscalationOwnerUserId=w.EscalationOwnerUserId,DueAtUtc=w.DueAtUtc,State=w.State,AcknowledgedAtUtc=w.AcknowledgedAtUtc,CompletedAtUtc=w.CompletedAtUtc,
   Action=action,Reason=reason.Trim(),EvidenceNoteId=noteId,EvidenceNoteRevision=noteRevision},ct:ct);
  await audit.RecordAsync(new(w.PatientId,"ClinicalFollowUpWork",w.Id,action,"committed",w.Revision.ToString(),"care"),ct,store);
  await outbox.WriteAsync(store,Guid.NewGuid(),"clinical.follow_up.changed",1,"ClinicalFollowUpWork",w.Id,w.Revision.ToString(),new{WorkId=w.Id,Revision=w.Revision,Action=action},ct);
 }
 public async Task<CommandReceipt> CreateInTransactionAsync(ICureFlowDbSession store,NamedFollowUpInput input,CancellationToken ct)
 {
  if(!store.IsInTransaction)throw new InvalidOperationException("Named follow-up creation requires the source transaction.");
  Reason(input.Reason);Assignment(input.AssignedUserId,input.EscalationOwnerUserId,input.DueAtUtc);
  if(input.PatientId==Guid.Empty||input.SourceId==Guid.Empty||input.SourceType!="appointment"||input.ExpectedAppointmentRevision is not >0)
   throw new ValidationException("Supply an exact patient appointment and current appointment revision.");
  await RequireAsync(store,CureFlowPermissions.FollowUpManage,input.PatientId,ct);
  var source=await store.QueryFirstOrDefaultAsync<Appointment>("SELECT * FROM \"Appointments\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@id AND \"PatientId\"=@patientId AND \"IsActive\" AND NOT \"IsDeleted\" FOR UPDATE",new{id=input.SourceId,patientId=input.PatientId},ct:ct)??throw new NotFoundException("Follow-up source appointment");
  if(source.Revision!=input.ExpectedAppointmentRevision)throw new ConflictException("The source appointment revision changed.");
  if(input.AssignedUserId.HasValue)await OwnerAsync(store,input.AssignedUserId.Value,CureFlowPermissions.FollowUpComplete,input.PatientId,ct);
  await OwnerAsync(store,input.EscalationOwnerUserId,CureFlowPermissions.FollowUpEscalate,input.PatientId,ct);
  var work=new ClinicalFollowUpWork{PatientId=input.PatientId,SourceId=source.Id,SourceVersion=source.Revision.ToString(),AssignedUserId=input.AssignedUserId,
   EscalationOwnerUserId=input.EscalationOwnerUserId,DueAtUtc=input.DueAtUtc};
  await store.InsertAsync(work,ct:ct);await EvidenceAsync(store,work,"create",input.Reason,null,null,ct);
  return new(work.Id,work.Revision,CommandOutcome.Created);
 }
 public Task<IdempotentCommandResult> CreateAsync(NamedFollowUpInput input,string key,CancellationToken ct)
  =>commands.ExecuteAsync("clinical.follow_up.create",key,input,Retry(),(store,token)=>RequireAsync(store,CureFlowPermissions.FollowUpManage,input.PatientId,token),
   async(store,token)=>(await CreateInTransactionAsync(store,input,token),201),ct);
 public async Task<NamedFollowUpView> GetAsync(Guid id,CancellationToken ct)
 {
  NamedFollowUpView result=null!;await db.TransactionAsync(async store=>{
   var work=await WorkAsync(store,id,false,ct);await RequireAsync(store,CureFlowPermissions.FollowUpView,work.PatientId,ct);
   await audit.RecordAsync(new(work.PatientId,"ClinicalFollowUpWork",id,"read","authorized",work.Revision.ToString(),"care"),ct,store);result=View(work);
  },ct);return result;
 }
 public async Task<IReadOnlyList<NamedFollowUpHistory>> HistoryAsync(Guid id,long? beforeRevision,CancellationToken ct)
 {
  if(beforeRevision is <=0)throw new ValidationException("Supply a positive history cursor.");
  IReadOnlyList<NamedFollowUpHistory> result=[];await db.TransactionAsync(async store=>{
   var work=await WorkAsync(store,id,false,ct);await RequireAsync(store,CureFlowPermissions.FollowUpView,work.PatientId,ct);
   var rows=await store.QueryAsync<ClinicalFollowUpVersion>("SELECT * FROM \"ClinicalFollowUpVersions\" WHERE \"TenantId\"=@TenantId AND \"WorkId\"=@id AND (@beforeRevision::bigint IS NULL OR \"Revision\"<@beforeRevision) ORDER BY \"Revision\" DESC LIMIT 100",new{id,beforeRevision},ct:ct);
   result=rows.Select(v=>new NamedFollowUpHistory(v.Revision,v.Action,v.Reason,v.CreatedBy,v.CreatedAt,v.AssignedUserId,v.EscalationOwnerUserId,v.DueAtUtc,v.State,v.EvidenceNoteId,v.EvidenceNoteRevision)).ToArray();
   await audit.RecordAsync(new(work.PatientId,"ClinicalFollowUpWork",id,"history","authorized",beforeRevision?.ToString(),"care"),ct,store);
  },ct);return result;
 }
 public async Task<IReadOnlyList<NamedFollowUpView>> EscalationsAsync(int limit,CancellationToken ct)
 {
  if(limit is <1 or >100)throw new ValidationException("Limit must be between 1 and 100.");
  IReadOnlyList<NamedFollowUpView> result=[];await db.TransactionAsync(async store=>{
   if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)throw new ForbiddenException();
   var rows=await store.QueryAsync<ClinicalFollowUpWork>("SELECT * FROM \"ClinicalFollowUpWorks\" WHERE \"TenantId\"=@TenantId AND \"EscalationOwnerUserId\"=@actor AND \"State\"<>'completed' AND (\"AssignedUserId\" IS NULL OR \"DueAtUtc\"<=clock_timestamp()) ORDER BY \"DueAtUtc\",\"Id\" LIMIT @limit",new{actor=tenant.UserId,limit},ct:ct);
   foreach(var row in rows){await RequireAsync(store,CureFlowPermissions.FollowUpEscalate,row.PatientId,ct);await RequireAsync(store,CureFlowPermissions.FollowUpView,row.PatientId,ct);
    await audit.RecordAsync(new(row.PatientId,"ClinicalFollowUpWork",row.Id,"escalation_read","authorized",row.Revision.ToString(),"care"),ct,store);}
   result=rows.Select(View).ToArray();
  },ct);return result;
 }
 private Task<IdempotentCommandResult> ChangeAsync<T>(Guid id,long revision,string action,string capability,T intent,string key,
  Func<ICureFlowDbSession,ClinicalFollowUpWork,CancellationToken,Task<(string Reason,Guid? NoteId,int? NoteRevision)>> change,CancellationToken ct)
 {
  if(revision<=0)throw new DomainException("Supply the current follow-up revision.",428);
  return commands.ExecuteAsync("clinical.follow_up."+action,key,new{id,revision,intent},Retry(),(store,token)=>AuthorizeAsync(store,id,capability,token),async(store,token)=>{
   var w=await WorkAsync(store,id,true,token);await RequireAsync(store,capability,w.PatientId,token);
   if(w.Revision!=revision||w.State=="completed")throw new ConflictException("Follow-up changed or is already completed.");
   var evidence=await change(store,w,token);w.Revision++;
   await store.ExecuteAsync("UPDATE \"ClinicalFollowUpWorks\" SET \"AssignedUserId\"=@AssignedUserId,\"EscalationOwnerUserId\"=@EscalationOwnerUserId,\"DueAtUtc\"=@DueAtUtc,\"State\"=@State,\"Revision\"=@Revision,\"AcknowledgedAtUtc\"=@AcknowledgedAtUtc,\"CompletedAtUtc\"=@CompletedAtUtc,\"UpdatedAt\"=CURRENT_TIMESTAMP,\"UpdatedBy\"=@actor WHERE \"TenantId\"=@TenantId AND \"Id\"=@Id",new{w.Id,w.AssignedUserId,w.EscalationOwnerUserId,w.DueAtUtc,w.State,w.Revision,w.AcknowledgedAtUtc,w.CompletedAtUtc,actor=tenant.UserId},ct:token);
   await EvidenceAsync(store,w,action,evidence.Reason,evidence.NoteId,evidence.NoteRevision,token);return(new CommandReceipt(w.Id,w.Revision,action=="complete"?CommandOutcome.Completed:CommandOutcome.Updated),200);
  },ct);
 }
 public Task<IdempotentCommandResult> AcknowledgeAsync(Guid id,long expectedRevision,string key,CancellationToken ct)
  =>ChangeAsync(id,expectedRevision,"acknowledge",CureFlowPermissions.FollowUpComplete,new{expectedRevision},key,async(store,w,token)=>{
   if(w.AssignedUserId!=tenant.UserId||w.State!="open")throw new ForbiddenException();
   await OwnerAsync(store,w.AssignedUserId.GetValueOrDefault(),CureFlowPermissions.FollowUpComplete,w.PatientId,token);
   w.State="acknowledged";w.AcknowledgedAtUtc=DateTime.UtcNow;return("Named assignee acknowledged ownership",null,null);
  },ct);
 public Task<IdempotentCommandResult> ReassignAsync(Guid id,NamedFollowUpChange input,string key,CancellationToken ct)
 {
  Reason(input.Reason);Assignment(input.AssignedUserId,input.EscalationOwnerUserId,input.DueAtUtc);
  return ChangeAsync(id,input.ExpectedRevision,"reassign",CureFlowPermissions.FollowUpManage,input,key,async(store,w,token)=>{
   if(input.AssignedUserId.HasValue)await OwnerAsync(store,input.AssignedUserId.Value,CureFlowPermissions.FollowUpComplete,w.PatientId,token);
   await OwnerAsync(store,input.EscalationOwnerUserId,CureFlowPermissions.FollowUpEscalate,w.PatientId,token);
   w.AssignedUserId=input.AssignedUserId;w.EscalationOwnerUserId=input.EscalationOwnerUserId;w.DueAtUtc=input.DueAtUtc;w.State="open";w.AcknowledgedAtUtc=null;
   return(input.Reason,null,null);
  },ct);
 }
 public Task<IdempotentCommandResult> CompleteAsync(Guid id,NamedFollowUpCompletion input,string key,CancellationToken ct)
 {
  Reason(input.Reason);var parts=input.EvidenceReference?.Split(':');
  if(parts is not {Length:3}||parts[0]!="clinical-note"||!Guid.TryParse(parts[1],out var noteId)||!int.TryParse(parts[2],out var noteRevision)||noteRevision<=0)
   throw new ValidationException("Completion requires clinical-note:<id>:<revision> finalized evidence.");
  return ChangeAsync(id,input.ExpectedRevision,"complete",CureFlowPermissions.FollowUpComplete,input,key,async(store,w,token)=>{
   if(w.AssignedUserId!=tenant.UserId||w.State!="acknowledged")throw new ForbiddenException();
   if(!await store.QueryFirstOrDefaultAsync<bool>("SELECT EXISTS(SELECT 1 FROM \"ClinicalNotes\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@noteId AND \"PatientId\"=@PatientId AND \"AppointmentId\"=@SourceId AND \"Revision\"=@noteRevision AND \"FinalizedAt\" IS NOT NULL AND \"EditableUntil\"<=clock_timestamp() AND \"IsActive\" AND NOT \"IsDeleted\")",new{noteId,noteRevision,w.PatientId,w.SourceId},ct:token))throw new ConflictException("Finalized completion evidence must match this patient's source appointment and current note revision.");
   w.State="completed";w.CompletedAtUtc=DateTime.UtcNow;return(input.Reason,(Guid?)noteId,(int?)noteRevision);
  },ct);
 }
}

using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public partial class VisitService
{
 private readonly IUserPermissionService _permissions;
 private readonly IIdempotentCommandExecutor _commands;
 private readonly IOutboxWriter _outbox;
 private readonly IConfiguration _configuration;
 private (string Version,TimeSpan Retry) EncounterPolicy()
 {
  var version=_configuration["Encounters:ApprovedPolicyVersion"];var hours=_configuration.GetValue<int?>("Encounters:ApprovedRetryHorizonHours");
  if(string.IsNullOrWhiteSpace(version)||version.Length>100||version.Any(char.IsControl)||hours is not >0 or >8760)
   throw new DomainException("The reviewed encounter lifecycle/retry policy is not configured.",503);
  return(version,TimeSpan.FromHours(hours.Value));
 }
 private static void EncounterReason(string? reason)
 {
  if(string.IsNullOrWhiteSpace(reason)||reason.Trim().Length is <3 or >1000||reason.Any(char.IsControl))throw new ValidationException("Supply a bounded attributable encounter reason.");
 }
 private static void EncounterContent(string? symptoms,string? diagnosis,string? advice,DateTime? followUp)
 {
  foreach(var text in new[]{symptoms,diagnosis,advice})
   if(text is not null&&(text.Length>4000||text.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t')))throw new ValidationException("Encounter content exceeds its bounds.");
  if(followUp.HasValue&&followUp.Value.Kind!=DateTimeKind.Utc)throw new ValidationException("Follow-up time must be UTC.");
 }
 private async Task<User> RequireEncounterAsync(ICureFlowDbSession store,Guid patientId,bool writing,CancellationToken ct)
 {
  if(!_tenant.IsAuthenticated||_tenant.IsPlatformUser||_tenant.ServiceIdentity is not null||_tenant.UserId is null||store.TenantId!=_tenant.TenantId)throw new ForbiddenException();
  var user=await store.QueryFirstOrDefaultAsync<User>("SELECT * FROM \"Users\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@id AND \"IsActive\" AND NOT \"IsDeleted\" FOR SHARE",new{id=_tenant.UserId},ct:ct)??throw new ForbiddenException();
  var codes=await _permissions.GetPermissionCodesAsync(user,ct,store);
  if(user.Role!=UserRole.Doctor||!codes.Contains(CureFlowPermissions.ClinicalView,StringComparer.Ordinal)
   ||writing&&!codes.Contains(CureFlowPermissions.ClinicalEdit,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal)
   ||!codes.Contains(CureFlowPermissions.AppointmentView,StringComparer.Ordinal))throw new ForbiddenException();
  if(!await store.QueryFirstOrDefaultAsync<bool>("SELECT public.cureflow_patient_read_allowed(@TenantId,@patientId)",new{patientId},ct:ct))throw new ForbiddenException();
  if(await store.GetByIdAsync<Patient>(patientId,ct:ct) is not {IsActive:true,IsDeleted:false})throw new NotFoundException("Patient");
  if(!await store.QueryFirstOrDefaultAsync<bool>("SELECT EXISTS(SELECT 1 FROM \"StaffProfiles\" WHERE \"TenantId\"=@TenantId AND \"UserId\"=@id AND \"IsActive\" AND NOT \"IsDeleted\")",new{id=user.Id},ct:ct))throw new ForbiddenException();
  return user;
 }
 private static async Task<Visit> EncounterAsync(ICureFlowDbSession store,Guid id,bool locked,CancellationToken ct)
  =>await store.QueryFirstOrDefaultAsync<Visit>($"SELECT * FROM \"Visits\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@id AND \"IsActive\" AND NOT \"IsDeleted\" {(locked?"FOR UPDATE":"")}",new{id},ct:ct)??throw new NotFoundException("Encounter");
 private async Task AuthorizeEncounterAsync(ICureFlowDbSession store,Guid id,bool writing,CancellationToken ct)
 {
  var visit=await EncounterAsync(store,id,false,ct);var actor=await RequireEncounterAsync(store,visit.PatientId,writing,ct);
  if(visit.DoctorUserId!=actor.Id)throw new ForbiddenException();
 }
 private static EncounterState State(Visit visit)=>new(visit.Id,visit.PatientId,visit.AppointmentId,visit.DoctorUserId,visit.Revision,
  visit.Status switch{VisitStatus.Scheduled=>"scheduled",VisitStatus.InProgress=>"in_progress",VisitStatus.Completed=>"completed",VisitStatus.Cancelled=>"cancelled",VisitStatus.NoShow=>"no_show",_=>throw new DomainException("Encounter state unavailable.",503)},visit.VisitDate,visit.EndedAt);
 private static string Snapshot(Visit visit)=>JsonSerializer.Serialize(new{visit.Symptoms,visit.Diagnosis,visit.FollowUpAdvice,visit.FollowUpDate});
 private async Task RecordEncounterAsync(ICureFlowDbSession store,Visit visit,int? previousStatus,string action,string reason,string policy,long? sourceRevision,CancellationToken ct)
 {
  await store.InsertAsync(new EncounterTransition{VisitId=visit.Id,PatientId=visit.PatientId,AppointmentId=visit.AppointmentId,Revision=visit.Revision,
   PreviousStatus=previousStatus,Status=(int)visit.Status,StartedAtUtc=visit.VisitDate,EndedAtUtc=visit.EndedAt,Action=action,Reason=reason.Trim(),
   PolicyVersion=policy,SourceAppointmentRevision=sourceRevision,ContentSnapshotJson=Snapshot(visit)},ct:ct);
  await _clinicalAudit.RecordAsync(new(visit.PatientId,"Visit",visit.Id,action,"committed",visit.Revision.ToString(),"care"),ct,store);
  await _outbox.WriteAsync(store,Guid.NewGuid(),"clinical.encounter.changed",1,"Visit",visit.Id,visit.Revision.ToString(),new{VisitId=visit.Id,Revision=visit.Revision,Action=action},ct);
 }
 public Task<IdempotentCommandResult> StartEncounterAsync(CreateVisitRequest input,string key,CancellationToken ct)
 {
  var policy=EncounterPolicy();EncounterReason(input.Reason);EncounterContent(input.Symptoms,input.Diagnosis,input.FollowUpAdvice,input.FollowUpDate);
  if(input.PatientId==Guid.Empty||input.AppointmentId is null||input.AppointmentId==Guid.Empty||input.ExpectedAppointmentRevision is not >0
   ||input.DoctorUserId==Guid.Empty||input.VisitType is not(null or "outpatient")||input.DoctorNotes is not null||input.Department is {Length:>120})
   throw new ValidationException("Start an outpatient encounter from an exact patient appointment/current revision; Doctor Notes remain separate.");
  return _commands.ExecuteAsync("encounter.start",key,input,policy.Retry,async(store,token)=>{
   var actor=await RequireEncounterAsync(store,input.PatientId,true,token);if(input.DoctorUserId.HasValue&&input.DoctorUserId!=actor.Id)throw new ForbiddenException();
  },async(store,token)=>{
   await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
   var actor=await RequireEncounterAsync(store,input.PatientId,true,token);
   var appointment=await store.QueryFirstOrDefaultAsync<Appointment>("SELECT * FROM \"Appointments\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@id AND \"PatientId\"=@patientId AND \"IsActive\" AND NOT \"IsDeleted\" FOR UPDATE",new{id=input.AppointmentId,patientId=input.PatientId},ct:token)??throw new NotFoundException("Encounter source appointment");
   if(appointment.DoctorUserId!=actor.Id||input.DoctorUserId.HasValue&&input.DoctorUserId!=actor.Id)throw new ForbiddenException();
   if(appointment.Revision!=input.ExpectedAppointmentRevision)throw new DomainException("The appointment changed. Review its current version.",412);
   var existing=await store.QueryAsync<Visit>("SELECT * FROM \"Visits\" WHERE \"TenantId\"=@TenantId AND \"AppointmentId\"=@id AND \"IsActive\" AND NOT \"IsDeleted\" ORDER BY \"Id\" LIMIT 2 FOR UPDATE",new{id=appointment.Id},ct:token);
   if(existing.Count>1)throw new ConflictException("Existing encounter links require explicit reconciliation before starting or resuming.");
   if(existing.Count==1)
   {
    var visit=existing[0];if(visit.PatientId!=input.PatientId||visit.DoctorUserId!=actor.Id)throw new ForbiddenException();
    if(visit.Status!=VisitStatus.InProgress||visit.EndedAt.HasValue)throw new ConflictException("The existing encounter is closed; starting cannot silently reopen it.");
    await _clinicalAudit.RecordAsync(new(visit.PatientId,"Visit",visit.Id,"resume","existing_open",visit.Revision.ToString(),"care"),token,store);
    return(new CommandReceipt(visit.Id,visit.Revision,CommandOutcome.Accepted),200);
   }
   if(appointment.Status is not(AppointmentStatus.Scheduled or AppointmentStatus.Confirmed)||!appointment.FacilityId.HasValue||!appointment.ReservationId.HasValue||!appointment.CheckedInAt.HasValue)
    throw new ConflictException("New encounters require an explicitly booked, arrived and open appointment.");
   if(await store.QueryFirstOrDefaultAsync<Guid?>("SELECT public.cureflow_lock_active_facility(@TenantId,@id)",new{id=appointment.FacilityId},ct:token)is null)throw new ConflictException("The source facility is unavailable.");
   var created=new Visit{PatientId=input.PatientId,AppointmentId=appointment.Id,DoctorUserId=actor.Id,DoctorName=actor.Name,Department=appointment.Department,
    VisitDate=DateTime.UtcNow,Status=VisitStatus.InProgress,VisitType="outpatient",ChiefComplaint=input.Symptoms,Symptoms=input.Symptoms,Diagnosis=input.Diagnosis,
    FollowUpAdvice=input.FollowUpAdvice,FollowUpDate=input.FollowUpDate,Revision=1};
   await store.InsertAsync(created,ct:token);await RecordEncounterAsync(store,created,null,"start",input.Reason!,policy.Version,appointment.Revision,token);
   return(new CommandReceipt(created.Id,created.Revision,CommandOutcome.Created),201);
  },ct);
 }
 public async Task<EncounterState> GetEncounterStateAsync(Guid id,CancellationToken ct)
 {
  EncounterState result=null!;await _db.TransactionAsync(async store=>{
   await AuthorizeEncounterAsync(store,id,false,ct);var visit=await EncounterAsync(store,id,false,ct);result=State(visit);
   await _clinicalAudit.RecordAsync(new(visit.PatientId,"Visit",id,"state","authorized",visit.Revision.ToString(),"care"),ct,store);
  },ct);return result;
 }
 public Task<IdempotentCommandResult> ResumeEncounterAsync(Guid id,EncounterResumeInput input,string key,CancellationToken ct)
 {
  var policy=EncounterPolicy();if(input.ExpectedRevision<=0)throw new DomainException("Supply the current encounter revision.",428);
  return _commands.ExecuteAsync("encounter.resume",key,new{id,input.ExpectedRevision},policy.Retry,(store,token)=>AuthorizeEncounterAsync(store,id,true,token),async(store,token)=>{
   var visit=await EncounterAsync(store,id,true,token);await AuthorizeEncounterAsync(store,id,true,token);
   if(visit.Revision!=input.ExpectedRevision)throw new DomainException("The encounter changed. Review its current version.",412);
   if(visit.Status!=VisitStatus.InProgress||visit.EndedAt.HasValue)throw new ConflictException("Only an open encounter can resume.");
   await _clinicalAudit.RecordAsync(new(visit.PatientId,"Visit",id,"resume","existing_open",visit.Revision.ToString(),"care"),token,store);
   return(new CommandReceipt(id,visit.Revision,CommandOutcome.Accepted),200);
  },ct);
 }
 private Task<IdempotentCommandResult> MutateEncounterAsync<T>(Guid id,long expectedRevision,string action,string reason,T intent,string key,
  Action<Visit> mutate,CancellationToken ct)
 {
  var policy=EncounterPolicy();EncounterReason(reason);if(expectedRevision<=0)throw new DomainException("Supply the current encounter revision.",428);
  return _commands.ExecuteAsync("encounter."+action,key,new{id,expectedRevision,intent},policy.Retry,(store,token)=>AuthorizeEncounterAsync(store,id,true,token),async(store,token)=>{
   await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
   var visit=await EncounterAsync(store,id,true,token);await AuthorizeEncounterAsync(store,id,true,token);
   if(visit.Revision!=expectedRevision)throw new DomainException("The encounter changed. Review its current version.",412);
   if(visit.Status!=VisitStatus.InProgress||visit.EndedAt.HasValue)throw new ConflictException("Closed encounters require reviewed correction, not ordinary mutation.");
   var previous=(int)visit.Status;mutate(visit);visit.Revision++;
   await store.ExecuteAsync("UPDATE \"Visits\" SET \"Symptoms\"=@Symptoms,\"Diagnosis\"=@Diagnosis,\"FollowUpAdvice\"=@FollowUpAdvice,\"FollowUpDate\"=@FollowUpDate,\"Status\"=@Status,\"EndedAt\"=@EndedAt,\"Revision\"=@Revision,\"UpdatedAt\"=CURRENT_TIMESTAMP,\"UpdatedBy\"=@actor WHERE \"TenantId\"=@TenantId AND \"Id\"=@Id",new{visit.Id,visit.Symptoms,visit.Diagnosis,visit.FollowUpAdvice,visit.FollowUpDate,Status=(int)visit.Status,visit.EndedAt,visit.Revision,actor=_tenant.UserId},ct:token);
   await RecordEncounterAsync(store,visit,previous,action,reason,policy.Version,null,token);
   return(new CommandReceipt(id,visit.Revision,action=="complete"?CommandOutcome.Completed:CommandOutcome.Updated),200);
  },ct);
 }
 public Task<IdempotentCommandResult> UpdateEncounterAsync(Guid id,UpdateVisitRequest input,string key,CancellationToken ct)
 {
  if(input.Status.HasValue||input.DoctorNotes is not null)throw new ValidationException("Use explicit encounter transitions and Doctor Notes; generic PATCH cannot set their states.");
  EncounterContent(input.Symptoms,input.Diagnosis,input.FollowUpAdvice,input.FollowUpDate);
  return MutateEncounterAsync(id,input.ExpectedRevision??0,"update",input.Reason!,input,key,visit=>{
   if(input.Symptoms is not null)visit.Symptoms=input.Symptoms;if(input.Diagnosis is not null)visit.Diagnosis=input.Diagnosis;
   if(input.FollowUpAdvice is not null)visit.FollowUpAdvice=input.FollowUpAdvice;if(input.FollowUpDate.HasValue)visit.FollowUpDate=input.FollowUpDate;
  },ct);
 }
 public Task<IdempotentCommandResult> CompleteEncounterAsync(Guid id,EncounterCompletionInput input,string key,CancellationToken ct)
  =>MutateEncounterAsync(id,input.ExpectedRevision,"complete",input.Reason,input,key,visit=>{visit.Status=VisitStatus.Completed;visit.EndedAt=DateTime.UtcNow;},ct);
 public async Task<IReadOnlyList<EncounterTransitionView>> EncounterHistoryAsync(Guid id,long? beforeRevision,CancellationToken ct)
 {
  if(beforeRevision is <=0)throw new ValidationException("Supply a positive history cursor.");
  IReadOnlyList<EncounterTransitionView> result=[];await _db.TransactionAsync(async store=>{
   await AuthorizeEncounterAsync(store,id,false,ct);var visit=await EncounterAsync(store,id,false,ct);
   var rows=await store.QueryAsync<EncounterTransition>("SELECT * FROM \"EncounterTransitions\" WHERE \"TenantId\"=@TenantId AND \"VisitId\"=@id AND(@beforeRevision::bigint IS NULL OR \"Revision\"<@beforeRevision) ORDER BY \"Revision\" DESC LIMIT 100",new{id,beforeRevision},ct:ct);
   result=rows.Select(r=>new EncounterTransitionView(r.Revision,r.Action,r.Reason,r.CreatedBy,r.CreatedAt,r.PreviousStatus,r.Status,r.EndedAtUtc)).ToArray();
   await _clinicalAudit.RecordAsync(new(visit.PatientId,"Visit",id,"history","authorized",beforeRevision?.ToString(),"care"),ct,store);
  },ct);return result;
 }
}

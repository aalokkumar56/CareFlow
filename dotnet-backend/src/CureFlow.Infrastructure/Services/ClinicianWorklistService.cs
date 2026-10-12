using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
namespace CureFlow.Infrastructure.Services;
public sealed class ClinicianWorklistService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
 IClinicalAccessAuditService audit):IClinicianWorklistService
{
 private sealed class Setting {public Guid Id{get;set;}public string Timezone{get;set;}="";}
 private sealed class Row
 {
  public Guid AppointmentId{get;set;}public Guid PatientId{get;set;}public long AppointmentRevision{get;set;}
  public DateTime ScheduledAtUtc{get;set;}public Guid? QueueEntryId{get;set;}public long? ArrivalToken{get;set;}
  public DateTime? ArrivedAtUtc{get;set;}public Guid? EncounterId{get;set;}public long? EncounterRevision{get;set;}
  public int? EncounterStatus{get;set;}public bool RequiresReconciliation{get;set;}
 }
 private static DateTime Boundary(DateOnly date,TimeZoneInfo zone)
 {
  var local=date.ToDateTime(TimeOnly.MinValue,DateTimeKind.Unspecified);
  if(zone.IsInvalidTime(local)||zone.IsAmbiguousTime(local))
   throw new DomainException("The facility day boundary requires reviewed timezone handling.",503);
  return TimeZoneInfo.ConvertTimeToUtc(local,zone);
 }
 public async Task<ClinicianWorklistPage> ListAsync(Guid facilityId,ClinicianWorklistQuery query,CancellationToken ct)
 {
  if(facilityId==Guid.Empty||query.Date==default||query.Date==DateOnly.MaxValue||query.Limit is <1 or >100
   ||query.AfterScheduledAtUtc.HasValue!=query.AfterAppointmentId.HasValue||query.AfterAppointmentId==Guid.Empty
   ||query.AfterScheduledAtUtc.HasValue&&query.AfterScheduledAtUtc.Value.Kind!=DateTimeKind.Utc)
   throw new ValidationException("Supply a facility, local date, limit 1–100 and an optional complete UTC appointment cursor.");
  ClinicianWorklistPage result=null!;
  await db.TransactionAsync(async store=>{
   if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null||store.TenantId!=tenant.TenantId)
    throw new ForbiddenException();
   var actor=await store.QueryFirstOrDefaultAsync<User>("""
    SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
    """,new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
   var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
   if(actor.Role!=UserRole.Doctor||!codes.Contains("Clinical.View",StringComparer.Ordinal)
    ||!codes.Contains("Appointment.View",StringComparer.Ordinal)||!codes.Contains("Patient.View",StringComparer.Ordinal)
    ||!await store.QuerySingleAsync<bool>("""
      SELECT EXISTS(SELECT 1 FROM "StaffProfiles" WHERE "TenantId"=@TenantId AND "UserId"=@id AND "IsActive" AND NOT "IsDeleted")
      """,new{id=actor.Id},ct:ct))throw new ForbiddenException();
   if(!await store.QuerySingleAsync<bool>("""
    SELECT EXISTS(SELECT 1 FROM "Facilities" WHERE "TenantId"=@TenantId AND "Id"=@facilityId AND "IsActive" AND NOT "IsDeleted")
    """,new{facilityId},ct:ct))throw new NotFoundException("Active facility");
   var setting=await store.QueryFirstOrDefaultAsync<Setting>("""
    SELECT "Id","Timezone" FROM "FacilityOperatingSettings" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId
     AND "EffectiveFrom"<=@now ORDER BY "EffectiveFrom" DESC,"Revision" DESC LIMIT 1
    """,new{facilityId,now=DateTime.UtcNow},ct:ct)??throw new DomainException("Configure the facility's current operating timezone.",503);
   TimeZoneInfo zone;
   try{zone=TimeZoneInfo.FindSystemTimeZoneById(setting.Timezone);}
   catch(TimeZoneNotFoundException){throw new DomainException("The facility timezone is unavailable.",503);}
   catch(InvalidTimeZoneException){throw new DomainException("The facility timezone is unavailable.",503);}
   var starts=Boundary(query.Date,zone);var ends=Boundary(query.Date.AddDays(1),zone);
   if(query.AfterScheduledAtUtc.HasValue&&(query.AfterScheduledAtUtc<starts||query.AfterScheduledAtUtc>=ends))
    throw new ValidationException("The cursor must fall within the selected facility day.");
   var rows=await store.QueryAsync<Row>("""
    SELECT a."Id" AS "AppointmentId",a."PatientId",a."Revision" AS "AppointmentRevision",a."ScheduledAt" AS "ScheduledAtUtc",
     q."Id" AS "QueueEntryId",q."Token" AS "ArrivalToken",q."ArrivedAtUtc",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Id" END AS "EncounterId",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Revision" END AS "EncounterRevision",
     CASE WHEN encounter.total=1 AND encounter.matches=1 THEN v."Status" END AS "EncounterStatus",
     (encounter.total>1 OR encounter.total<>encounter.matches) AS "RequiresReconciliation"
    FROM "Appointments" a
    LEFT JOIN "QueueEntries" q ON q."TenantId"=a."TenantId" AND q."AppointmentId"=a."Id" AND q."PatientId"=a."PatientId" AND q."FacilityId"=a."FacilityId"
    LEFT JOIN LATERAL(SELECT count(*) AS total,count(*) FILTER(WHERE e."PatientId"=a."PatientId" AND e."DoctorUserId"=@actorId) AS matches
     FROM "Visits" e WHERE e."TenantId"=a."TenantId" AND e."AppointmentId"=a."Id" AND e."IsActive" AND NOT e."IsDeleted")encounter ON true
    LEFT JOIN LATERAL(SELECT e."Id",e."Revision",e."Status" FROM "Visits" e WHERE e."TenantId"=a."TenantId" AND e."AppointmentId"=a."Id"
     AND e."PatientId"=a."PatientId" AND e."DoctorUserId"=@actorId AND e."IsActive" AND NOT e."IsDeleted"
     ORDER BY e."Id" LIMIT 1)v ON true
    WHERE a."TenantId"=@TenantId AND a."FacilityId"=@facilityId AND a."DoctorUserId"=@actorId AND a."IsActive" AND NOT a."IsDeleted"
     AND a."Status" IN(0,1,2) AND a."ScheduledAt">=@starts AND a."ScheduledAt"<@ends
     AND(q."Id" IS NOT NULL OR encounter.matches>0)
     AND(@afterScheduledAtUtc::timestamptz IS NULL OR(a."ScheduledAt",a."Id")>(@afterScheduledAtUtc,@afterAppointmentId::uuid))
     AND public.cureflow_patient_read_allowed(a."TenantId",a."PatientId")
    ORDER BY a."ScheduledAt",a."Id" LIMIT @take
    """,new{facilityId,actorId=actor.Id,starts,ends,query.AfterScheduledAtUtc,query.AfterAppointmentId,take=query.Limit+1},ct:ct);
   var items=rows.Take(query.Limit).Select(r=>new ClinicianWorklistItem(r.AppointmentId,r.PatientId,r.AppointmentRevision,r.ScheduledAtUtc,
    r.QueueEntryId,r.ArrivalToken,r.ArrivedAtUtc,r.EncounterId,r.EncounterRevision,r.RequiresReconciliation?"needs_review":r.EncounterStatus switch{
     0=>"scheduled",1=>"in_progress",2=>"completed",3=>"cancelled",4=>"no_show",null=>"arrived",_=>"needs_review"},
    r.RequiresReconciliation||r.EncounterStatus is <0 or >4)).ToArray();
   foreach(var item in items)await audit.RecordAsync(new(item.PatientId,"ClinicianWorklist",item.EncounterId??item.AppointmentId,
    "read_assigned_worklist","authorized",(item.EncounterRevision??item.AppointmentRevision).ToString(),"care"),ct,store);
   if(items.Length==0)await audit.RecordAsync(new(null,"Facility",facilityId,"read_assigned_worklist","authorized",null,"care"),ct,store);
   result=new(facilityId,actor.Id,query.Date,setting.Timezone,setting.Id,starts,ends,items,
    rows.Count>query.Limit?new(items[^1].ScheduledAtUtc,items[^1].AppointmentId):null);
  },ct);
  return result;
 }
}

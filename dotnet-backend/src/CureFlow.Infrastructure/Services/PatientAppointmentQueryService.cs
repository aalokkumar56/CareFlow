using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
namespace CureFlow.Infrastructure.Services;
public sealed class PatientAppointmentQueryService(ICureFlowDbSession db,ITenantContext tenant,
 IUserPermissionService permissions,IClinicalAccessAuditService audit):IPatientAppointmentQueryService
{
 private sealed class Row
 {
  public Guid Id{get;set;}
  public Guid PatientId{get;set;}
  public Guid? FacilityId{get;set;}
  public Guid? PractitionerUserId{get;set;}
  public DateTime ScheduledAtUtc{get;set;}
  public int DurationMinutes{get;set;}
  public int Status{get;set;}
  public long Revision{get;set;}
  public string? TimeZoneId{get;set;}
 }
 private async Task RequireAsync(ICureFlowDbSession store,Guid patientId,Guid? facilityId,CancellationToken ct)
 {
  if(patientId==Guid.Empty||facilityId==Guid.Empty)throw new ValidationException("Supply an explicit patient and valid optional facility.");
  if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null||store.TenantId!=tenant.TenantId)throw new ForbiddenException();
  var actor=await store.QueryFirstOrDefaultAsync<User>("SELECT * FROM \"Users\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@id AND \"IsActive\" AND NOT \"IsDeleted\" FOR SHARE",new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
  var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
  if(!codes.Contains(CureFlowPermissions.AppointmentView,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal))throw new ForbiddenException();
  if(!await store.QueryFirstOrDefaultAsync<bool>("SELECT public.cureflow_patient_read_allowed(@TenantId,@patientId)",new{patientId},ct:ct))throw new ForbiddenException();
  if(await store.GetByIdAsync<Patient>(patientId,ct:ct) is not {IsDeleted:false})throw new NotFoundException("Patient");
  if(facilityId.HasValue&&!await store.QueryFirstOrDefaultAsync<bool>("SELECT EXISTS(SELECT 1 FROM \"Facilities\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@facilityId AND NOT \"IsDeleted\")",new{facilityId},ct:ct))throw new NotFoundException("Facility");
 }
 private const string Projection="""
  SELECT a."Id",a."PatientId",a."FacilityId",a."DoctorUserId" AS "PractitionerUserId",a."ScheduledAt" AS "ScheduledAtUtc",
   a."DurationMinutes",a."Status",a."Revision",setting."Timezone" AS "TimeZoneId"
  FROM "Appointments" a
  LEFT JOIN LATERAL(SELECT s."Timezone" FROM "FacilityOperatingSettings" s
   WHERE s."TenantId"=a."TenantId" AND s."FacilityId"=a."FacilityId" AND s."EffectiveFrom"<=a."ScheduledAt"
   ORDER BY s."EffectiveFrom" DESC,s."Revision" DESC LIMIT 1)setting ON true
  WHERE a."TenantId"=@TenantId AND a."PatientId"=@patientId AND a."IsActive" AND NOT a."IsDeleted"
   AND(@facilityId::uuid IS NULL OR a."FacilityId"=@facilityId)
  """;
 private static string StatusName(int status)=>status switch{0=>"scheduled",1=>"confirmed",2=>"completed",3=>"no_show",4=>"cancelled",_=>throw new DomainException("Appointment state is unavailable.",503)};
 private static PatientAppointmentView View(Row row)
 {
  DateTimeOffset? local=null;string? zoneId=null;
  // Missing legacy location/settings stay unknown; never infer primary facility or use the server timezone.
  if(row.FacilityId.HasValue&&!string.IsNullOrWhiteSpace(row.TimeZoneId))
  {
   try{var zone=TimeZoneInfo.FindSystemTimeZoneById(row.TimeZoneId);local=TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(row.ScheduledAtUtc,DateTimeKind.Utc)),zone);zoneId=zone.Id;}
   catch(TimeZoneNotFoundException){}catch(InvalidTimeZoneException){}
  }
  return new(row.Id,row.PatientId,row.FacilityId,row.PractitionerUserId,row.ScheduledAtUtc,local,zoneId,row.DurationMinutes,StatusName(row.Status),row.Revision);
 }
 public async Task<PatientAppointmentPage> ListAsync(Guid patientId,PatientAppointmentQuery query,CancellationToken ct)
 {
  if(query.FromUtc.Kind!=DateTimeKind.Utc||query.ToUtc.Kind!=DateTimeKind.Utc||query.ToUtc<=query.FromUtc
   ||query.ToUtc-query.FromUtc>TimeSpan.FromDays(366)||query.Limit is <1 or >100
   ||query.AfterScheduledAtUtc.HasValue!=query.AfterAppointmentId.HasValue||query.AfterAppointmentId==Guid.Empty
   ||query.AfterScheduledAtUtc.HasValue&&(query.AfterScheduledAtUtc.Value.Kind!=DateTimeKind.Utc||query.AfterScheduledAtUtc.Value<query.FromUtc||query.AfterScheduledAtUtc.Value>=query.ToUtc))
   throw new ValidationException("Supply a UTC half-open window of at most 366 days, limit 1–100 and an optional complete cursor within that window.");
  int? status=query.Status switch{null=>null,"scheduled"=>0,"confirmed"=>1,"completed"=>2,"no_show"=>3,"cancelled"=>4,_=>throw new ValidationException("Supply a supported appointment status.")};
  PatientAppointmentPage result=null!;await db.TransactionAsync(async store=>{
   await RequireAsync(store,patientId,query.FacilityId,ct);
   var rows=await store.QueryAsync<Row>(Projection+"""
    AND a."ScheduledAt">=@fromUtc AND a."ScheduledAt"<@toUtc AND(@status::integer IS NULL OR a."Status"=@status)
    AND(@afterScheduledAtUtc::timestamptz IS NULL OR(a."ScheduledAt",a."Id")>(@afterScheduledAtUtc,@afterAppointmentId::uuid))
    ORDER BY a."ScheduledAt",a."Id" LIMIT @take
    """,new{patientId,query.FacilityId,query.FromUtc,query.ToUtc,status,query.AfterScheduledAtUtc,query.AfterAppointmentId,take=query.Limit+1},ct:ct);
   var page=rows.Take(query.Limit).Select(View).ToArray();
   var cursor=rows.Count>query.Limit?new PatientAppointmentCursor(page[^1].ScheduledAtUtc,page[^1].Id):null;
   result=new(page,cursor,query.FromUtc,query.ToUtc);
   await audit.RecordAsync(new(patientId,"PatientAppointments",patientId,"list","authorized",null,"care"),ct,store);
  },ct);return result;
 }
 public async Task<PatientNextAppointment> NextAsync(Guid patientId,Guid? facilityId,CancellationToken ct)
 {
  PatientNextAppointment result=null!;await db.TransactionAsync(async store=>{
   await RequireAsync(store,patientId,facilityId,ct);var now=DateTime.UtcNow;
   // The instant decides future eligibility; local calendar midnight cannot select yesterday's or cancelled work.
   var row=await store.QueryFirstOrDefaultAsync<Row>(Projection+"""
    AND a."Status" IN(0,1) AND a."ScheduledAt">=@now
    ORDER BY a."ScheduledAt",a."Id" LIMIT 1
    """,new{patientId,facilityId,now},ct:ct);
   result=new(now,row is null?null:View(row));
   await audit.RecordAsync(new(patientId,"PatientAppointments",row?.Id??patientId,"next","authorized",row?.Revision.ToString(),"care"),ct,store);
  },ct);return result;
 }
}

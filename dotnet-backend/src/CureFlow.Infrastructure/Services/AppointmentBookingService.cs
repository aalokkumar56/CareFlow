using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class AppointmentBookingService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
 IIdempotentCommandExecutor commands,IAppointmentReservationService reservations,IClinicalAccessAuditService audit,IConfiguration config):IAppointmentBookingService
{
    private (string Version,TimeSpan Retry) Policy()
    {
        var version=config["AppointmentBookings:ApprovedPolicyVersion"];
        var hours=config.GetValue<int?>("AppointmentBookings:ApprovedRetryHorizonHours");
        if(string.IsNullOrWhiteSpace(version)||version.Length>100||version.Any(char.IsControl)||hours is not >0 or >8760)
            throw new DomainException("The reviewed appointment booking/retry policy is not configured.",503);
        return(version,TimeSpan.FromHours(hours.Value));
    }
    private async Task RequireAsync(ICureFlowDbSession store,bool writing,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)throw new ForbiddenException();
        var user=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(user,ct,store);
        if(!codes.Contains(writing?CureFlowPermissions.AppointmentCreate:CureFlowPermissions.AppointmentView,StringComparer.Ordinal)
            ||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.StaffView,StringComparer.Ordinal))throw new ForbiddenException();
    }
    private static Task<Patient> PatientAsync(ICureFlowDbSession store,Guid id,CancellationToken ct)=>VisibleAsync<Patient>(store,"Patients",id,ct);
    private static async Task<T> VisibleAsync<T>(ICureFlowDbSession store,string table,Guid id,CancellationToken ct)
        =>await store.QueryFirstOrDefaultAsync<T>($"""SELECT * FROM "{table}" WHERE "TenantId"=@TenantId AND "Id"=@id AND NOT "IsDeleted" AND "IsActive" FOR SHARE""",
            new{id},ct:ct)??throw new NotFoundException(typeof(T).Name);
    private static void Validate(AppointmentBookingInput input)
    {
        if(input.PatientId==Guid.Empty||input.ReservationId==Guid.Empty||input.ExpectedReservationRevision<1
            ||input.BookingKind is not ("routine" or "walk_in" or "follow_up")
            ||(input.BookingKind=="follow_up")!=input.FollowUpAppointmentId.HasValue
            ||input.FollowUpAppointmentId==Guid.Empty||input.ChiefComplaint?.Length>1000||input.Notes?.Length>4000
            ||input.ChiefComplaint?.Any(c=>char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t')==true
            ||input.Notes?.Any(c=>char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t')==true)
            throw new ValidationException("Supply a patient, current hold revision, routine/walk_in/follow_up type and bounded optional notes; follow_up requires its source appointment.");
    }
    public Task<IdempotentCommandResult> BookAsync(AppointmentBookingInput input,string key,CancellationToken ct)
    {
        Validate(input);var policy=Policy();
        return commands.ExecuteAsync("appointment.booking.create",key,input,policy.Retry,
            async(store,token)=>{await RequireAsync(store,true,token);await PatientAsync(store,input.PatientId,token);},
            async(store,token)=>(await BookInTransactionAsync(store,input,token),201),ct);
    }
    // Reused only by an authorized source command with its own idempotent transaction.
    public async Task<CommandReceipt> BookInTransactionAsync(ICureFlowDbSession store,AppointmentBookingInput input,CancellationToken token)
    {
        if(!store.IsInTransaction||store.TenantId!=tenant.TenantId||store.UserId!=tenant.UserId)throw new ForbiddenException();
        Validate(input);var policy=Policy();
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
                await RequireAsync(store,true,token);
                var patient=await PatientAsync(store,input.PatientId,token);
                var hold=await store.QueryFirstOrDefaultAsync<AppointmentReservation>("""
                    SELECT * FROM "AppointmentReservations" WHERE "TenantId"=@TenantId AND "Id"=@id AND "OwnerUserId"=@owner FOR UPDATE
                    """,new{id=input.ReservationId,owner=tenant.UserId},ct:token)??throw new NotFoundException("Reservation");
                if(hold.State!="held"||hold.Revision!=input.ExpectedReservationRevision||hold.HoldExpiresAtUtc<=DateTime.UtcNow)
                    throw new ConflictException("The reservation changed or expired; reserve again.");
                if(!await store.QueryFirstOrDefaultAsync<bool>("""
                    SELECT EXISTS(SELECT 1 FROM "PatientIdentifiers" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId
                        AND "FacilityId"=@facilityId AND "Namespace"='local-mrn')
                    """,new{patientId=patient.Id,facilityId=hold.FacilityId},ct:token))
                    throw new ConflictException("Issue this patient's local identifier in the explicit booking facility before booking.");
                if(input.FollowUpAppointmentId.HasValue)
                {
                    var prior=await VisibleAsync<Appointment>(store,"Appointments",input.FollowUpAppointmentId.Value,token);
                    if(prior.PatientId!=patient.Id||prior.ScheduledAt>=hold.StartsAtUtc||prior.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
                        throw new ConflictException("Follow-up must reference an earlier current appointment of this same patient.");
                }
                var practitioner=await VisibleAsync<User>(store,"Users",hold.PractitionerUserId,token);
                var profile=await store.QueryFirstOrDefaultAsync<StaffProfile>("""
                    SELECT * FROM "StaffProfiles" WHERE "TenantId"=@TenantId AND "UserId"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
                    """,new{id=practitioner.Id},ct:token)??throw new NotFoundException("Staff profile");
                var appointment=new Appointment{PatientId=patient.Id,PatientName=patient.Name,PatientPhone=patient.Phone,
                    FacilityId=hold.FacilityId,ReservationId=hold.Id,BookingKind=input.BookingKind,FollowUpAppointmentId=input.FollowUpAppointmentId,
                    BookingPolicyVersion=policy.Version,DoctorUserId=practitioner.Id,DoctorName=practitioner.Name,
                    Department=profile.Department??practitioner.Specialty??"",ScheduledAt=hold.StartsAtUtc,
                    DurationMinutes=(int)(hold.EndsAtUtc-hold.StartsAtUtc).TotalMinutes,ChiefComplaint=input.ChiefComplaint,Notes=input.Notes,
                    ConsultationFee=profile.ConsultationFee,Status=AppointmentStatus.Scheduled,Revision=1};
                await store.InsertAsync(appointment,ct:token);
                await reservations.ConfirmInTransactionAsync(store,hold.Id,input.ExpectedReservationRevision,appointment.Id,token);
                await audit.RecordAsync(new(patient.Id,"Appointment",appointment.Id,"book","committed","1","booking"),token,store);
                return new CommandReceipt(appointment.Id,1,CommandOutcome.Created);
    }
    public async Task<AppointmentBookingView> GetAsync(Guid id,CancellationToken ct)
    {
        AppointmentBookingView? result=null;
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,false,ct);
            var a=await VisibleAsync<Appointment>(store,"Appointments",id,ct);
            if(a.FacilityId is null||a.ReservationId is null||a.DoctorUserId is null||a.BookingKind is null)
                throw new ConflictException("Historical appointments have no inferred facility/reservation; use their retained legacy record.");
            await PatientAsync(store,a.PatientId,ct);
            result=new(a.Id,a.PatientId,a.FacilityId.Value,a.ReservationId.Value,a.DoctorUserId.Value,a.ScheduledAt,
                a.ScheduledAt.AddMinutes(a.DurationMinutes),a.BookingKind,a.FollowUpAppointmentId,a.Status,a.Revision,
                $"\"appointment:{a.Id:D}:{a.Revision}\"");
            await audit.RecordAsync(new(a.PatientId,"Appointment",a.Id,"read","authorized",a.Revision.ToString(),"booking"),ct,store);
        },ct);
        return result!;
    }
}
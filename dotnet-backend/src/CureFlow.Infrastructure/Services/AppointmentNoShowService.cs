using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class AppointmentNoShowService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,INamedFollowUpService followUps,IClinicalAccessAuditService audit,IConfiguration config):IAppointmentNoShowService
{
    private (string Version,int Cutoff,TimeSpan Retry) Policy()
    {
        var version=config["NoShows:ApprovedPolicyVersion"];
        var cutoff=config.GetValue<int?>("NoShows:ApprovedCutoffMinutesAfterScheduledStart");
        var hours=config.GetValue<int?>("NoShows:ApprovedRetryHorizonHours");
        if(string.IsNullOrWhiteSpace(version)||version.Length>100||version.Any(char.IsControl)
            ||cutoff is null or <0 or >10080||hours is null or <=0 or >8760)
            throw new DomainException("Configure the approved no-show cutoff and retry policy.",503);
        return(version,cutoff.Value,TimeSpan.FromHours(hours.Value));
    }
    private async Task RequireAsync(ICureFlowDbSession store,Guid facilityId,bool writing,CancellationToken ct)
    {
        if(facilityId==Guid.Empty||!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)
            throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
        if(!codes.Contains(writing?"Appointment.Edit":"Appointment.View",StringComparer.Ordinal)
            ||!codes.Contains("Patient.View",StringComparer.Ordinal)||(writing&&!codes.Contains("FollowUp.Manage",StringComparer.Ordinal)))
            throw new ForbiddenException();
        if(!await store.QuerySingleAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "Facilities" WHERE "TenantId"=@TenantId AND "Id"=@facilityId AND NOT "IsDeleted"
                AND (@writing=false OR "IsActive"))
            """,new{facilityId,writing},ct:ct))throw new NotFoundException("Facility");
    }
    private static async Task<Appointment> SourceAsync(ICureFlowDbSession store,Guid facilityId,Guid appointmentId,bool locked,CancellationToken ct)
        =>await store.QueryFirstOrDefaultAsync<Appointment>($"""
            SELECT * FROM "Appointments" WHERE "TenantId"=@TenantId AND "Id"=@appointmentId AND "FacilityId"=@facilityId
                AND "IsActive" AND NOT "IsDeleted" {(locked?"FOR UPDATE":"")}
            """,new{facilityId,appointmentId},ct:ct)??throw new NotFoundException("Appointment");
    public Task<IdempotentCommandResult> RecordAsync(Guid facilityId,Guid appointmentId,long expectedRevision,AppointmentNoShowInput input,string key,CancellationToken ct)
    {
        var policy=Policy();
        if(appointmentId==Guid.Empty||expectedRevision<1||input.AssignedUserId==Guid.Empty||input.EscalationOwnerUserId==Guid.Empty
            ||input.FollowUpDueAtUtc.Kind!=DateTimeKind.Utc
            ||string.IsNullOrWhiteSpace(input.Reason)||input.Reason.Trim().Length<3||input.Reason.Length>500||input.Reason.Any(char.IsControl))
            throw new ValidationException("Supply named follow-up and escalation owners, a future UTC due time and bounded reason.");
        return commands.ExecuteAsync("appointment.no_show",key,new{facilityId,appointmentId,expectedRevision,input.AssignedUserId,
            input.EscalationOwnerUserId,input.FollowUpDueAtUtc,reason=input.Reason.Trim()},policy.Retry,
            async(store,token)=>{await RequireAsync(store,facilityId,true,token);_=await SourceAsync(store,facilityId,appointmentId,false,token);},
            async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
                await RequireAsync(store,facilityId,true,token);
                if(await store.QueryFirstOrDefaultAsync<Guid?>("SELECT public.cureflow_lock_active_facility(@TenantId,@facilityId)",new{facilityId},ct:token)is null)
                    throw new ConflictException("The facility is unavailable.");
                var appointment=await SourceAsync(store,facilityId,appointmentId,true,token);
                if(appointment.Revision!=expectedRevision)throw new DomainException("The appointment changed. Review its current state and version.",412);
                var now=DateTime.UtcNow;
                if(input.FollowUpDueAtUtc<=now)throw new ValidationException("Supply a future follow-up due time.");
                if(appointment.Status is not(AppointmentStatus.Scheduled or AppointmentStatus.Confirmed)
                    ||appointment.CheckedInAt.HasValue||appointment.CompletedAt.HasValue||!appointment.ReservationId.HasValue)
                    throw new ConflictException("Only an unchecked-in pending reserved appointment can become a no-show.");
                if(now<appointment.ScheduledAt.AddMinutes(policy.Cutoff))throw new ConflictException("The approved no-show cutoff has not elapsed.");
                // Task source pins the exact pre-transition appointment; the action ledger links its final revision.
                var work=await followUps.CreateInTransactionAsync(store,new(appointment.PatientId,"appointment",appointment.Id,
                    appointment.Revision,input.AssignedUserId,input.EscalationOwnerUserId,input.FollowUpDueAtUtc,input.Reason.Trim()),token);
                var record=new AppointmentNoShow{AppointmentId=appointment.Id,PatientId=appointment.PatientId,FacilityId=facilityId,
                    ReservationId=appointment.ReservationId.Value,FollowUpWorkId=work.ResourceId,OriginalAppointmentRevision=appointment.Revision,
                    AppointmentRevision=checked(appointment.Revision+1),ScheduledAtUtc=appointment.ScheduledAt,DurationMinutes=appointment.DurationMinutes,
                    CutoffMinutesAfterStart=policy.Cutoff,RecordedAtUtc=now,Reason=input.Reason.Trim(),PolicyVersion=policy.Version};
                await store.InsertAsync(record,ct:token);
                if(await store.ExecuteAsync("""
                    UPDATE "AppointmentReservations" SET "State"='released',"Revision"="Revision"+1,"UpdatedAt"=@now
                    WHERE "TenantId"=@TenantId AND "Id"=@id AND "State"='confirmed' AND "AppointmentId"=@appointmentId
                    """,new{id=appointment.ReservationId,appointmentId,now},ct:token)!=1)throw new ConflictException("The current appointment allocation changed.");
                await store.ExecuteAsync("""
                    UPDATE "AppointmentReservationSeats" SET "State"='released',"UpdatedAt"=@now
                    WHERE "TenantId"=@TenantId AND "ReservationId"=@reservation AND "State"='confirmed'
                    """,new{reservation=appointment.ReservationId,now},ct:token);
                if(await store.ExecuteAsync("""
                    UPDATE "Appointments" SET "Status"=@status,"Revision"=@revision,"UpdatedAt"=@now,"UpdatedBy"=@actor
                    WHERE "TenantId"=@TenantId AND "Id"=@appointmentId AND "Revision"=@expectedRevision AND "CheckedInAt" IS NULL
                    """,new{status=(int)AppointmentStatus.NoShow,revision=record.AppointmentRevision,now,actor=tenant.UserId,appointmentId,expectedRevision},ct:token)!=1)
                    throw new DomainException("Check-in or another appointment change won the conflict. Review the current state.",412);
                await audit.RecordAsync(new(record.PatientId,"Appointment",appointmentId,"record_no_show","committed",record.AppointmentRevision.ToString()),token,store);
                return(new CommandReceipt(appointmentId,record.AppointmentRevision,CommandOutcome.Updated),200);
            },ct);
    }
    public async Task<AppointmentNoShowView> GetAsync(Guid facilityId,Guid appointmentId,CancellationToken ct)
    {
        await RequireAsync(db,facilityId,false,ct);
        var record=await db.QueryFirstOrDefaultAsync<AppointmentNoShow>("""
            SELECT * FROM "AppointmentNoShows" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "AppointmentId"=@appointmentId
            """,new{facilityId,appointmentId},ct:ct)??throw new NotFoundException("No-show evidence");
        await audit.RecordAsync(new(record.PatientId,"AppointmentNoShow",record.Id,"read","authorized","1"),ct);
        return new(record.Id,record.AppointmentId,record.PatientId,record.FacilityId,record.FollowUpWorkId,
            record.OriginalAppointmentRevision,record.AppointmentRevision,record.ScheduledAtUtc,record.CutoffMinutesAfterStart,
            record.RecordedAtUtc,record.Reason,record.PolicyVersion,record.CreatedBy);
    }
}

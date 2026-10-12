using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class AppointmentBookingChangeService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,IIdempotentCommandExecutor commands,
 IAppointmentReservationService reservations,IClinicalAccessAuditService audit,IOutboxWriter outbox,IConfiguration config):IAppointmentBookingChangeService
{
    private (string Version,TimeSpan Retry) Policy()
    {
        var version=config["AppointmentBookingChanges:ApprovedPolicyVersion"];
        var hours=config.GetValue<int?>("AppointmentBookingChanges:ApprovedRetryHorizonHours");
        if(string.IsNullOrWhiteSpace(version)||version.Length>100||version.Any(char.IsControl)||hours is not >0 or >8760)
            throw new DomainException("The reviewed cancellation/reschedule retry policy is not configured.",503);
        return(version,TimeSpan.FromHours(hours.Value));
    }
    private async Task RequireAsync(ICureFlowDbSession store,Guid id,bool reschedule,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)throw new ForbiddenException();
        var user=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(user,ct,store);
        if(!codes.Contains(CureFlowPermissions.AppointmentEdit,StringComparer.Ordinal)
            ||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal)
            ||!codes.Contains(CureFlowPermissions.StaffView,StringComparer.Ordinal)
            ||reschedule&&!codes.Contains(CureFlowPermissions.AppointmentCreate,StringComparer.Ordinal))throw new ForbiddenException();
        var appointment=await store.GetByIdAsync<Appointment>(id,ct:ct)??throw new NotFoundException("Appointment");
        if(await store.GetByIdAsync<Patient>(appointment.PatientId,ct:ct) is null)throw new NotFoundException("Patient");
    }
    public async Task<IReadOnlyList<AppointmentBookingChangeView>> HistoryAsync(Guid id,long afterRevision,int limit,CancellationToken ct)
    {
        if(afterRevision<0||limit is <1 or >100)throw new ValidationException("Supply bounded history cursor and limit.");
        IReadOnlyList<AppointmentBookingChangeView> result=Array.Empty<AppointmentBookingChangeView>();
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,id,false,ct);
            var a=await store.GetByIdAsync<Appointment>(id,ct:ct)??throw new NotFoundException("Appointment");
            var rows=await store.QueryAsync<AppointmentBookingChange>("""
                SELECT * FROM "AppointmentBookingChanges" WHERE "TenantId"=@TenantId AND "AppointmentId"=@id
                 AND "AppointmentRevision">@afterRevision ORDER BY "AppointmentRevision" LIMIT @limit
                """,new{id,afterRevision,limit},ct:ct);
            result=rows.Select(c=>new AppointmentBookingChangeView(c.Id,c.AppointmentRevision,c.Action,c.PreviousReservationId,c.ReplacementReservationId,
                c.PreviousStartsAtUtc,c.PreviousEndsAtUtc,c.ReplacementStartsAtUtc,c.ReplacementEndsAtUtc,c.Reason,c.CreatedAt,c.CreatedBy)).ToArray();
            await audit.RecordAsync(new(a.PatientId,"AppointmentBookingChange",id,"history","authorized",a.Revision.ToString(),"booking-change"),ct,store);
        },ct);return result;
    }
    public Task<IdempotentCommandResult> CancelAsync(Guid id,long expectedRevision,AppointmentCancelInput input,string key,CancellationToken ct)
        =>ChangeAsync(id,expectedRevision,null,null,input.Reason,key,ct);
    public Task<IdempotentCommandResult> RescheduleAsync(Guid id,long expectedRevision,AppointmentRescheduleInput input,string key,CancellationToken ct)
        =>ChangeAsync(id,expectedRevision,input.ReservationId,input.ExpectedReservationRevision,input.Reason,key,ct);
    private Task<IdempotentCommandResult> ChangeAsync(Guid id,long expectedRevision,Guid? replacementId,long? expectedHoldRevision,string reason,string key,CancellationToken ct)
    {
        if(id==Guid.Empty||expectedRevision<1||replacementId==Guid.Empty||replacementId.HasValue&&expectedHoldRevision is not >0
            ||string.IsNullOrWhiteSpace(reason)||reason.Trim().Length is <3 or >1000||reason.Any(char.IsControl))
            throw new ValidationException("Supply current appointment/hold revisions and a bounded accountable reason.");
        var policy=Policy();var reschedule=replacementId.HasValue;
        return commands.ExecuteAsync(reschedule?"appointment.booking.reschedule":"appointment.booking.cancel",key,
            new{id,expectedRevision,replacementId,expectedHoldRevision,reason=reason.Trim()},policy.Retry,
            (store,token)=>RequireAsync(store,id,reschedule,token),async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
                await RequireAsync(store,id,reschedule,token);
                var a=await store.QueryFirstOrDefaultAsync<Appointment>("""
                    SELECT * FROM "Appointments" WHERE "TenantId"=@TenantId AND "Id"=@id AND NOT "IsDeleted" FOR UPDATE
                    """,new{id},ct:token)??throw new NotFoundException("Appointment");
                if(a.Revision!=expectedRevision)throw new DomainException("Appointment changed; reload its current ETag.",412);
                if(a.ReservationId is null||a.FacilityId is null||a.CheckedInAt.HasValue||a.CompletedAt.HasValue
                    ||a.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed)||a.ScheduledAt<=DateTime.UtcNow)
                    throw new ConflictException("Only a future unchecked-in explicit booking can be changed; retained legacy or active care needs reviewed workflow.");
                var oldHold=await store.QueryFirstOrDefaultAsync<AppointmentReservation>("""
                    SELECT * FROM "AppointmentReservations" WHERE "TenantId"=@TenantId AND "Id"=@id FOR UPDATE
                    """,new{id=a.ReservationId},ct:token)??throw new NotFoundException("Original allocation");
                if(oldHold.State!="confirmed"||oldHold.AppointmentId!=a.Id)throw new ConflictException("Original confirmed allocation does not match appointment.");
                AppointmentReservation? next=null;User? practitioner=null;StaffProfile? profile=null;
                if(reschedule)
                {
                    next=await store.QueryFirstOrDefaultAsync<AppointmentReservation>("""
                        SELECT * FROM "AppointmentReservations" WHERE "TenantId"=@TenantId AND "Id"=@id AND "OwnerUserId"=@owner FOR UPDATE
                        """,new{id=replacementId,owner=tenant.UserId},ct:token)??throw new NotFoundException("Replacement reservation");
                    if(next.State!="held"||next.Revision!=expectedHoldRevision||next.HoldExpiresAtUtc<=DateTime.UtcNow||next.StartsAtUtc<=DateTime.UtcNow)
                        throw new ConflictException("Replacement must be a future current live owned hold.");
                    if(!await store.QueryFirstOrDefaultAsync<bool>("""
                        SELECT EXISTS(SELECT 1 FROM "PatientIdentifiers" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId
                            AND "FacilityId"=@facilityId AND "Namespace"='local-mrn')
                        """,new{patientId=a.PatientId,facilityId=next.FacilityId},ct:token))throw new ConflictException("Patient requires the replacement facility's local identifier.");
                    if(a.FollowUpAppointmentId.HasValue)
                    {
                        var prior=await store.GetByIdAsync<Appointment>(a.FollowUpAppointmentId.Value,ct:token)??throw new NotFoundException("Follow-up source");
                        if(prior.PatientId!=a.PatientId||prior.ScheduledAt>=next.StartsAtUtc||prior.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
                            throw new ConflictException("Replacement must remain after this patient's retained follow-up source.");
                    }
                    practitioner=await store.GetByIdAsync<User>(next.PractitionerUserId,ct:token)??throw new NotFoundException("Practitioner");
                    profile=await store.QueryFirstOrDefaultAsync<StaffProfile>("""
                        SELECT * FROM "StaffProfiles" WHERE "TenantId"=@TenantId AND "UserId"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
                        """,new{id=next.PractitionerUserId},ct:token)??throw new NotFoundException("Staff profile");
                }
                if(await store.QueryFirstOrDefaultAsync<bool>("""
                    SELECT EXISTS(SELECT 1 FROM "Appointments" child WHERE child."TenantId"=@TenantId AND child."FollowUpAppointmentId"=@id
                     AND child."Status" IN(0,1) AND child."IsActive" AND NOT child."IsDeleted" AND (@cancel OR child."ScheduledAt"<=@starts))
                    """,new{id=a.Id,cancel=!reschedule,starts=next?.StartsAtUtc??a.ScheduledAt},ct:token))
                    throw new ConflictException("Reconcile active follow-up references before changing their prior source.");
                var change=new AppointmentBookingChange{AppointmentId=a.Id,PatientId=a.PatientId,AppointmentRevision=a.Revision+1,
                    Action=reschedule?"reschedule":"cancel",PreviousReservationId=oldHold.Id,ReplacementReservationId=next?.Id,
                    PreviousStartsAtUtc=a.ScheduledAt,PreviousEndsAtUtc=a.ScheduledAt.AddMinutes(a.DurationMinutes),
                    ReplacementStartsAtUtc=next?.StartsAtUtc,ReplacementEndsAtUtc=next?.EndsAtUtc,Reason=reason.Trim(),PolicyVersion=policy.Version};
                await store.InsertAsync(change,ct:token);
                await store.ExecuteAsync("""
                    UPDATE "AppointmentReservations" SET "State"='released',"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "Id"=@id
                    """,new{id=oldHold.Id},ct:token);
                await store.ExecuteAsync("""
                    UPDATE "AppointmentReservationSeats" SET "State"='released',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "ReservationId"=@id
                    """,new{id=oldHold.Id},ct:token);
                if(next is not null)
                {
                    await store.ExecuteAsync("""
                        UPDATE "Appointments" SET "FacilityId"=@facility,"ReservationId"=@reservation,"DoctorUserId"=@practitionerId,"DoctorName"=@name,
                         "Department"=@department,"ScheduledAt"=@starts,"DurationMinutes"=@duration,"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP
                        WHERE "TenantId"=@TenantId AND "Id"=@id
                        """,new{id=a.Id,facility=next.FacilityId,reservation=next.Id,practitionerId=next.PractitionerUserId,name=practitioner!.Name,
                            department=profile!.Department??practitioner.Specialty??"",starts=next.StartsAtUtc,duration=(int)(next.EndsAtUtc-next.StartsAtUtc).TotalMinutes},ct:token);
                    await reservations.ConfirmInTransactionAsync(store,next.Id,expectedHoldRevision!.Value,a.Id,token);
                }
                else await store.ExecuteAsync("""
                    UPDATE "Appointments" SET "Status"=@status,"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "Id"=@id
                    """,new{id=a.Id,status=(int)AppointmentStatus.Cancelled},ct:token);
                await audit.RecordAsync(new(a.PatientId,"Appointment",a.Id,change.Action,"committed",change.AppointmentRevision.ToString(),"booking-change"),token,store);
                // Notice intent is committed once with the transition. Reviewed delivery adapters run later;
                // this transaction never sends messages or SignalR effects.
                await outbox.WriteAsync(store,Guid.NewGuid(),"appointment.booking-changed",1,"Appointment",a.Id,change.AppointmentRevision.ToString(),
                    new{ChangeId=change.Id,AppointmentId=a.Id,Revision=change.AppointmentRevision},token);
                return(new CommandReceipt(a.Id,change.AppointmentRevision,CommandOutcome.Updated),200);
            },ct);
    }
}
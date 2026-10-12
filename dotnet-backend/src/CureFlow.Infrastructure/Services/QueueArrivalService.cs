using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Organization;
using CureFlow.Domain.Enums;
using Microsoft.Extensions.Configuration;
using CureFlow.Infrastructure.Identity;
namespace CureFlow.Infrastructure.Services;
public sealed class QueueArrivalService(ICureFlowDbSession db, ITenantContext tenant,
    IUserPermissionService permissions, IIdempotentCommandExecutor commands,
    IClinicalAccessAuditService audit, IConfiguration config) : IQueueArrivalService
{
    private (TimeSpan Retry, int Early, int Late) Policy()
    {
        var retry=config.GetValue<int?>("QueueArrivals:ApprovedRetryHorizonHours");
        var early=config.GetValue<int?>("QueueArrivals:ApprovedEarlyArrivalMinutes");
        var late=config.GetValue<int?>("QueueArrivals:ApprovedLateArrivalMinutes");
        if(retry is null or <=0 or >8760 || early is null or <0 or >1440 || late is null or <0 or >1440)
            throw new DomainException("The arrival window and retry policy are not configured.",503);
        return(TimeSpan.FromHours(retry.Value),early.Value,late.Value);
    }
    private async Task RequireAsync(ICureFlowDbSession store, Guid facilityId, bool writing, CancellationToken ct)
    {
        if(facilityId==Guid.Empty || !tenant.IsAuthenticated || tenant.IsPlatformUser || tenant.ServiceIdentity is not null || tenant.UserId is null)
            throw new ForbiddenException();
        var user=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(user,ct,store);
        if(!codes.Contains(writing?"Appointment.Edit":"Appointment.View",StringComparer.Ordinal)
            || !codes.Contains("Patient.View",StringComparer.Ordinal)) throw new ForbiddenException();
        if(!await store.QuerySingleAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "Facilities" WHERE "TenantId"=@TenantId AND "Id"=@facilityId
                AND "IsActive" AND NOT "IsDeleted")
            """,new{facilityId},ct:ct))throw new NotFoundException("Active facility");
    }
    private static Task<Appointment?> AppointmentAsync(ICureFlowDbSession store, Guid facilityId, Guid appointmentId,
        bool locked,CancellationToken ct)=>store.QueryFirstOrDefaultAsync<Appointment>($"""
        SELECT * FROM "Appointments" WHERE "TenantId"=@TenantId AND "Id"=@appointmentId AND "FacilityId"=@facilityId
            AND "IsActive" AND NOT "IsDeleted" {(locked?"FOR UPDATE":"")}
        """,new{facilityId,appointmentId},ct:ct);
    private static Task<QueueEntry?> EntryAsync(ICureFlowDbSession store,Guid facilityId,Guid appointmentId,CancellationToken ct)
        =>store.QueryFirstOrDefaultAsync<QueueEntry>("""
            SELECT * FROM "QueueEntries" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "AppointmentId"=@appointmentId
            """,new{facilityId,appointmentId},ct:ct);
    public Task<IdempotentCommandResult> CheckInAsync(Guid facilityId,Guid appointmentId,long expectedRevision,string key,CancellationToken ct)
    {
        var policy=Policy();
        if(appointmentId==Guid.Empty||expectedRevision<1)throw new ValidationException("Supply an appointment and current revision.");
        return commands.ExecuteAsync("appointment.arrival",key,new{facilityId,appointmentId,expectedRevision},policy.Retry,
            async(store,token)=>{
                await RequireAsync(store,facilityId,true,token);
                _=await AppointmentAsync(store,facilityId,appointmentId,false,token)??throw new NotFoundException("Appointment");
            },async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
                if(await store.QueryFirstOrDefaultAsync<Guid?>("SELECT public.cureflow_lock_active_facility(@TenantId,@facilityId)",new{facilityId},ct:token)is null)
                    throw new ConflictException("The facility is unavailable.");
                var appointment=await AppointmentAsync(store,facilityId,appointmentId,true,token)??throw new NotFoundException("Appointment");
                var previous=await EntryAsync(store,facilityId,appointmentId,token);
                if(previous is not null){
                    await audit.RecordAsync(new(previous.PatientId,"QueueEntry",previous.Id,"check_in","existing","1"),token,store);
                    return(new CommandReceipt(previous.Id,1,CommandOutcome.Completed),200);
                }
                if(appointment.Revision!=expectedRevision)throw new DomainException("The appointment changed. Review its current version.",412);
                if(appointment.Status is not(AppointmentStatus.Scheduled or AppointmentStatus.Confirmed)
                    ||appointment.CheckedInAt.HasValue||!appointment.ReservationId.HasValue)
                    throw new ConflictException("Only an explicitly reserved pending appointment can enter this queue.");
                var now=DateTime.UtcNow;
                if(now<appointment.ScheduledAt.AddMinutes(-policy.Early)||now>appointment.ScheduledAt.AddMinutes(policy.Late))
                    throw new ConflictException("Arrival is outside the configured appointment window.");
                var settings=await store.QueryFirstOrDefaultAsync<FacilityOperatingSetting>("""
                    SELECT * FROM "FacilityOperatingSettings" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId
                    ORDER BY "Revision" DESC LIMIT 1
                    """,new{facilityId},ct:token)??throw new ConflictException("Facility operating settings are unavailable.");
                TimeZoneInfo zone;
                try{zone=TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);}
                catch(TimeZoneNotFoundException){throw new ConflictException("The facility timezone is unavailable.");}
                catch(InvalidTimeZoneException){throw new ConflictException("The facility timezone is unavailable.");}
                var date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now,zone));
                var count=await store.QuerySingleAsync<long>("""
                    INSERT INTO "QueueCounters" ("Id","TenantId","FacilityId","ArrivalDate","LastToken","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted")
                    VALUES (@id,@TenantId,@facilityId,@date,1,@now,@now,@actor,@actor,true,false)
                    ON CONFLICT ("TenantId","FacilityId","ArrivalDate") DO UPDATE
                    SET "LastToken"="QueueCounters"."LastToken"+1,"UpdatedAt"=@now,"UpdatedBy"=@actor
                    RETURNING "LastToken"
                    """,new{id=Guid.NewGuid(),facilityId,date,now,actor=tenant.UserId},ct:token);
                var revision=appointment.Revision+1;
                var entry=new QueueEntry{AppointmentId=appointmentId,PatientId=appointment.PatientId,FacilityId=facilityId,
                    ReservationId=appointment.ReservationId.Value,OperatingSettingId=settings.Id,Timezone=settings.Timezone,
                    ArrivalDate=date,ArrivedAtUtc=now,Token=count,AppointmentRevision=revision};
                if(await store.ExecuteAsync("""
                    UPDATE "Appointments" SET "CheckedInAt"=@now,"Revision"=@revision,"UpdatedAt"=@now,"UpdatedBy"=@actor
                    WHERE "TenantId"=@TenantId AND "Id"=@appointmentId AND "Revision"=@expectedRevision AND "CheckedInAt" IS NULL
                    """,new{appointmentId,expectedRevision,revision,now,actor=tenant.UserId},ct:token)!=1)
                    throw new DomainException("The appointment changed. Review its current version.",412);
                await store.InsertAsync(entry,ct:token);
                await audit.RecordAsync(new(entry.PatientId,"QueueEntry",entry.Id,"check_in","committed","1"),token,store);
                return(new CommandReceipt(entry.Id,1,CommandOutcome.Created),201);
            },ct);
    }
    private static QueueArrivalView View(QueueEntry entry)=>new(entry.Id,entry.AppointmentId,entry.PatientId,entry.FacilityId,
        entry.ArrivalDate,entry.Token,entry.ArrivedAtUtc,entry.Timezone,entry.AppointmentRevision);
    public async Task<QueueArrivalView> GetAsync(Guid facilityId,Guid appointmentId,CancellationToken ct)
    {
        await RequireAsync(db,facilityId,false,ct);
        var entry=await EntryAsync(db,facilityId,appointmentId,ct)??throw new NotFoundException("Queue arrival");
        await audit.RecordAsync(new(entry.PatientId,"QueueEntry",entry.Id,"read","authorized","1"),ct);
        return View(entry);
    }
    public async Task<IReadOnlyList<QueueArrivalView>> ListAsync(Guid facilityId,DateOnly date,long? afterToken,int take,CancellationToken ct)
    {
        if(date==default||afterToken is <0||take is <1 or >100)throw new ValidationException("Supply a queue date, nonnegative cursor and limit from 1 to 100.");
        await RequireAsync(db,facilityId,false,ct);
        var rows=await db.QueryAsync<QueueEntry>("""
            SELECT * FROM "QueueEntries" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "ArrivalDate"=@date
                AND "Token">@after ORDER BY "Token" LIMIT @take
            """,new{facilityId,date,after=afterToken??0,take},ct:ct);
        foreach(var entry in rows)await audit.RecordAsync(new(entry.PatientId,"QueueEntry",entry.Id,"read","authorized","1"),ct);
        return rows.Select(View).ToArray();
    }
}

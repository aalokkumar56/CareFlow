using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Organization;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class AppointmentReservationService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IConfiguration config):IAppointmentReservationService
{
    private record Target(string Kind,Guid Id,int Capacity);
    private (string Version,TimeSpan Retry,int Minutes,int AdvanceDays,int PastMinutes,int MaximumActive) Policy()
    {
        var version=config["AppointmentReservations:ApprovedPolicyVersion"];
        var hours=config.GetValue<int?>("AppointmentReservations:ApprovedRetryHorizonHours");
        var minutes=config.GetValue<int?>("AppointmentReservations:ApprovedHoldMinutes");
        var advance=config.GetValue<int?>("AppointmentReservations:ApprovedMaximumAdvanceDays");
        var past=config.GetValue<int?>("AppointmentReservations:ApprovedPastStartGraceMinutes");
        var active=config.GetValue<int?>("AppointmentReservations:ApprovedMaximumActiveHoldsPerActor");
        if(string.IsNullOrWhiteSpace(version)||version.Length>100||version.Any(char.IsControl)||hours is not >0 or >8760
            ||minutes is not >0 or >30||advance is not >0 or >366||past is null or <0 or >15||active is not >0 or >100)
            throw new DomainException("The reservation policy/hold/advance/retry limits are not configured.",503);
        return(version,TimeSpan.FromHours(hours.Value),minutes.Value,advance.Value,past.Value,active.Value);
    }
    private async Task RequireAsync(ICureFlowDbSession store,bool writing,CancellationToken ct,bool clinical=false)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)throw new ForbiddenException();
        var user=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(user,ct,store);
        if(!codes.Contains(writing?CureFlowPermissions.AppointmentCreate:CureFlowPermissions.AppointmentView,StringComparer.Ordinal)
            ||!codes.Contains(CureFlowPermissions.StaffView,StringComparer.Ordinal)||(clinical&&!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal)))throw new ForbiddenException();
    }
    private static async Task LockAsync(ICureFlowDbSession store,IEnumerable<Target> targets,CancellationToken ct)
    {
        await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:ct);
        foreach(var target in targets.OrderBy(t=>t.Kind,StringComparer.Ordinal).ThenBy(t=>t.Id.ToString("N"),StringComparer.Ordinal))
            await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",
                new{scope=$"appointment-seat:{store.TenantId:D}:{target.Kind}:{target.Id:D}"},ct:ct);
    }
    private async Task<AppointmentReservation> OwnedAsync(ICureFlowDbSession store,Guid id,bool locked,CancellationToken ct)
        =>await store.QueryFirstOrDefaultAsync<AppointmentReservation>($"""
            SELECT * FROM "AppointmentReservations" WHERE "TenantId"=@TenantId AND "Id"=@id AND "OwnerUserId"=@owner {(locked?"FOR UPDATE":"")}
            """,new{id,owner=tenant.UserId},ct:ct)??throw new NotFoundException("Reservation");
    private static async Task<List<Target>> TargetsAsync(ICureFlowDbSession store,AppointmentHoldInput input,CancellationToken ct)
    {
        if(!await store.QueryFirstOrDefaultAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "Users" u JOIN "StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
              WHERE u."TenantId"=@TenantId AND u."Id"=@id AND u."IsActive" AND NOT u."IsDeleted"
                AND u."Role"=ANY(@roles) AND p."IsActive" AND NOT p."IsDeleted" AND p."IsAvailable")
            """,new{id=input.PractitionerUserId,roles=new[]{(int)UserRole.Doctor,(int)UserRole.Nurse,(int)UserRole.Staff}},ct:ct))
            throw new ConflictException("Practitioner is not current eligible available staff.");
        var result=new List<Target>{new("practitioner",input.PractitionerUserId,1)};
        foreach(var id in input.ResourceIds.OrderBy(id=>id.ToString("N"),StringComparer.Ordinal))
        {
            var resource=await store.QueryFirstOrDefaultAsync<FacilityResource>("""
                SELECT * FROM "FacilityResources" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@id
                  AND "IsActive" AND NOT "IsDeleted" AND "OperationalStatus"='enabled' FOR SHARE
                """,new{input.FacilityId,id},ct:ct)??throw new NotFoundException("Enabled facility resource");
            if(resource.ResourceType is not ("room" or "equipment")||resource.Capacity is <1 or >100000
                ||input.StartsAtUtc<resource.EffectiveFrom||resource.EffectiveUntil.HasValue&&input.EndsAtUtc>resource.EffectiveUntil)
                throw new ConflictException("Selected room/equipment is not eligible for this entire interval.");
            result.Add(new("resource",id,resource.Capacity));
        }
        return result;
    }
    private async Task ExpireAsync(ICureFlowDbSession store,IReadOnlyList<Target> targets,CancellationToken ct)
    {
        var targetIds=targets.Select(t=>t.Id).ToArray();
        var stale=await store.QueryAsync<AppointmentReservation>("""
            SELECT * FROM "AppointmentReservations" r WHERE r."TenantId"=@TenantId AND r."State"='held' AND r."HoldExpiresAtUtc"<=clock_timestamp()
              AND EXISTS(SELECT 1 FROM "AppointmentReservationSeats" s WHERE s."TenantId"=r."TenantId" AND s."ReservationId"=r."Id" AND s."TargetId"=ANY(@targetIds))
            ORDER BY r."Id" LIMIT 500 FOR UPDATE
            """,new{targetIds},ct:ct);
        foreach(var hold in stale)
        {
            await store.ExecuteAsync("""
                UPDATE "AppointmentReservations" SET "State"='expired',"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "Id"=@id
                """,new{id=hold.Id},ct:ct);
            await store.ExecuteAsync("""
                UPDATE "AppointmentReservationSeats" SET "State"='expired',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "ReservationId"=@id
                """,new{id=hold.Id},ct:ct);
            await audit.RecordAsync(new(null,"AppointmentReservation",hold.Id,"expire","committed",(hold.Revision+1).ToString(),"operations"),ct,store);
        }
    }
    public Task<IdempotentCommandResult> HoldAsync(AppointmentHoldInput input,string key,CancellationToken ct)
    {
        var policy=Policy();
        if(input.FacilityId==Guid.Empty||input.PractitionerUserId==Guid.Empty||input.ResourceIds is null||input.ResourceIds.Count>5
            ||input.ResourceIds.Any(id=>id==Guid.Empty)||input.ResourceIds.Distinct().Count()!=input.ResourceIds.Count
            ||input.StartsAtUtc.Kind!=DateTimeKind.Utc||input.EndsAtUtc.Kind!=DateTimeKind.Utc||input.EndsAtUtc<=input.StartsAtUtc
            ||(input.EndsAtUtc-input.StartsAtUtc).TotalMinutes is <1 or >480
            ||(input.EndsAtUtc-input.StartsAtUtc).Ticks%TimeSpan.TicksPerMinute!=0
            ||input.StartsAtUtc<DateTime.UtcNow.AddMinutes(-policy.PastMinutes)||input.EndsAtUtc<=DateTime.UtcNow
            ||input.StartsAtUtc>DateTime.UtcNow.AddDays(policy.AdvanceDays))
            throw new ValidationException("Supply explicit facility/practitioner, at most five resources and a bounded UTC whole-minute interval.");
        return commands.ExecuteAsync("appointment.reservation.hold",key,new{input.FacilityId,input.PractitionerUserId,ResourceIds=input.ResourceIds.OrderBy(id=>id.ToString("N"),StringComparer.Ordinal).ToArray(),input.StartsAtUtc,input.EndsAtUtc},policy.Retry,
            (store,token)=>RequireAsync(store,true,token),async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
                if(!((await store.QueryFirstOrDefaultAsync<Guid?>("SELECT public.cureflow_lock_active_facility(@TenantId,@id)",new{id=input.FacilityId},ct:token)).HasValue))
                    throw new NotFoundException("Active facility");
                var targets=await TargetsAsync(store,input,token);await LockAsync(store,targets,token);
                await LegacyAppointmentOccupancy.RequireAvailableAsync(store,input.PractitionerUserId,input.StartsAtUtc,input.EndsAtUtc,token);
                await ReservationScheduleCoverage.RequireAsync(store,input.FacilityId,input.PractitionerUserId,input.ResourceIds.ToArray(),input.StartsAtUtc,input.EndsAtUtc,token);
                await ExpireAsync(store,targets,token);
                if(await store.QueryFirstOrDefaultAsync<int>("""
                    SELECT count(*)::integer FROM "AppointmentReservations" WHERE "TenantId"=@TenantId AND "OwnerUserId"=@owner
                      AND "State"='held' AND "HoldExpiresAtUtc">clock_timestamp()
                    """,new{owner=tenant.UserId},ct:token)>=policy.MaximumActive)throw new ConflictException("Release an existing hold before creating another.");
                var now=DateTime.UtcNow;var expiry=now.AddMinutes(policy.Minutes);
                if(expiry>input.EndsAtUtc)expiry=input.EndsAtUtc;
                if(expiry<=now)throw new ConflictException("Selected interval ended before reservation.");
                var hold=new AppointmentReservation{OwnerUserId=tenant.UserId!.Value,FacilityId=input.FacilityId,
                    PractitionerUserId=input.PractitionerUserId,StartsAtUtc=input.StartsAtUtc,EndsAtUtc=input.EndsAtUtc,HoldExpiresAtUtc=expiry,PolicyVersion=policy.Version};
                await store.InsertAsync(hold,ct:token);
                foreach(var target in targets)
                {
                    var seat=await store.QueryFirstOrDefaultAsync<int?>("""
                        SELECT n FROM generate_series(1,@capacity) n WHERE NOT EXISTS(
                          SELECT 1 FROM "AppointmentReservationSeats" s WHERE s."TenantId"=@TenantId AND s."TargetType"=@kind AND s."TargetId"=@id
                            AND s."Seat"=n AND s."State" IN ('held','confirmed') AND s."StartsAtUtc"<@ends AND s."EndsAtUtc">@starts) ORDER BY n LIMIT 1
                        """,new{capacity=target.Capacity,kind=target.Kind,id=target.Id,starts=input.StartsAtUtc,ends=input.EndsAtUtc},ct:token);
                    if(!seat.HasValue)throw new ConflictException("Selected practitioner/resource capacity is unavailable.");
                    await store.InsertAsync(new AppointmentReservationSeat{ReservationId=hold.Id,TargetType=target.Kind,TargetId=target.Id,
                        Seat=seat.Value,CapacitySnapshot=target.Capacity,StartsAtUtc=input.StartsAtUtc,EndsAtUtc=input.EndsAtUtc},ct:token);
                }
                await audit.RecordAsync(new(null,"AppointmentReservation",hold.Id,"hold","committed","1","operations"),token,store);
                return(new CommandReceipt(hold.Id,1,CommandOutcome.Created),201);
            },ct);
    }
    public async Task<AppointmentHoldView> GetAsync(Guid id,CancellationToken ct)
    {
        AppointmentHoldView? result=null;
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,false,ct);var hold=await OwnedAsync(store,id,false,ct);
            Guid? patientId=null;
            if(hold.AppointmentId.HasValue)
            {
                await RequireAsync(store,false,ct,true);
                var appointment=await store.GetByIdAsync<Appointment>(hold.AppointmentId.Value,ct:ct)??throw new NotFoundException("Patient appointment");
                patientId=appointment.PatientId;
            }
            var seats=await store.QueryAsync<AppointmentReservationSeat>("""
                SELECT * FROM "AppointmentReservationSeats" WHERE "TenantId"=@TenantId AND "ReservationId"=@id ORDER BY "TargetId"
                """,new{id},ct:ct);
            result=new(id,hold.FacilityId,hold.PractitionerUserId,hold.StartsAtUtc,hold.EndsAtUtc,hold.HoldExpiresAtUtc,
                hold.State=="held"&&hold.HoldExpiresAtUtc<=DateTime.UtcNow?"expired":hold.State,hold.Revision,
                seats.Where(s=>s.TargetType=="resource").Select(s=>s.TargetId).ToArray());
            await audit.RecordAsync(new(patientId,"AppointmentReservation",id,"read","authorized",hold.Revision.ToString(),"operations"),ct,store);
        },ct);return result!;
    }
    public Task<IdempotentCommandResult> ReleaseAsync(Guid id,long expectedRevision,string key,CancellationToken ct)
    {
        if(expectedRevision<1)throw new ValidationException("Supply the current reservation revision.");
        var policy=Policy();
        return commands.ExecuteAsync("appointment.reservation.release",key,new{id,expectedRevision},policy.Retry,
            async(store,token)=>{await RequireAsync(store,true,token);await OwnedAsync(store,id,false,token);},async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
                var hold=await OwnedAsync(store,id,true,token);
                if(hold.Revision!=expectedRevision)throw new DomainException("Reservation changed; reload.",412);
                if(hold.State!="held")throw new ConflictException("Only a held reservation may be released here; confirmed bookings require their cancellation command.");
                var state=hold.HoldExpiresAtUtc<=DateTime.UtcNow?"expired":"released";
                await store.ExecuteAsync("""
                    UPDATE "AppointmentReservations" SET "State"=@state,"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "Id"=@id
                    """,new{id,state},ct:token);
                await store.ExecuteAsync("""
                    UPDATE "AppointmentReservationSeats" SET "State"=@state,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "ReservationId"=@id
                    """,new{id,state},ct:token);
                await audit.RecordAsync(new(null,"AppointmentReservation",id,"release","committed",(hold.Revision+1).ToString(),"operations"),token,store);
                return(new CommandReceipt(id,hold.Revision+1,CommandOutcome.Updated),200);
            },ct);
    }
    public async Task ConfirmInTransactionAsync(ICureFlowDbSession store,Guid id,long expectedRevision,Guid appointmentId,CancellationToken ct)
    {
        if(!store.IsInTransaction||store.TenantId!=tenant.TenantId||store.UserId!=tenant.UserId)throw new ForbiddenException();
        await RequireAsync(store,true,ct,true);await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:ct);
        var hold=await OwnedAsync(store,id,true,ct);
        if(hold.State!="held"||hold.Revision!=expectedRevision||hold.HoldExpiresAtUtc<=DateTime.UtcNow)throw new ConflictException("Reservation is stale, expired or no longer held.");
        var resourceIds=(await store.QueryAsync<Guid>("""
            SELECT "TargetId" FROM "AppointmentReservationSeats" WHERE "TenantId"=@TenantId AND "ReservationId"=@id AND "TargetType"='resource'
            """,new{id},ct:ct)).ToArray();
        var input=new AppointmentHoldInput(hold.FacilityId,hold.PractitionerUserId,resourceIds,hold.StartsAtUtc,hold.EndsAtUtc);
        if(!(await store.QueryFirstOrDefaultAsync<Guid?>("SELECT public.cureflow_lock_active_facility(@TenantId,@facility)",new{facility=hold.FacilityId},ct:ct)).HasValue)
            throw new NotFoundException("Active facility");
        await LockAsync(store,await TargetsAsync(store,input,ct),ct);
        await LegacyAppointmentOccupancy.RequireAvailableAsync(store,hold.PractitionerUserId,hold.StartsAtUtc,hold.EndsAtUtc,ct);
        await ReservationScheduleCoverage.RequireAsync(store,hold.FacilityId,hold.PractitionerUserId,resourceIds,hold.StartsAtUtc,hold.EndsAtUtc,ct);
        var appointment=await store.GetByIdAsync<Appointment>(appointmentId,ct:ct)??throw new NotFoundException("Patient appointment");
        if(appointment.FacilityId!=hold.FacilityId||appointment.ReservationId!=hold.Id||appointment.DoctorUserId!=hold.PractitionerUserId||appointment.ScheduledAt!=hold.StartsAtUtc
            ||appointment.DurationMinutes!=(int)(hold.EndsAtUtc-hold.StartsAtUtc).TotalMinutes||appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
            throw new ConflictException("Appointment must match the exact held practitioner and interval.");
        var confirmed=await store.ExecuteAsync("""
            UPDATE "AppointmentReservations" SET "State"='confirmed',"AppointmentId"=@appointmentId,"Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP
              WHERE "TenantId"=@TenantId AND "Id"=@id AND "HoldExpiresAtUtc">clock_timestamp()
            """,new{id,appointmentId},ct:ct);
        if(confirmed!=1)throw new ConflictException("Reservation expired before confirmation.");
        await store.ExecuteAsync("""
            UPDATE "AppointmentReservationSeats" SET "State"='confirmed',"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "ReservationId"=@id
            """,new{id},ct:ct);
        await audit.RecordAsync(new(appointment.PatientId,"AppointmentReservation",id,"confirm","committed",(hold.Revision+1).ToString(),"booking"),ct,store);
    }
}
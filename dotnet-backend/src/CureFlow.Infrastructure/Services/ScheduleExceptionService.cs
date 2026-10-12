using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Validation;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Organization;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;

namespace CureFlow.Infrastructure.Services;

public sealed class ScheduleExceptionService(ICureFlowDbSession db, ITenantContext tenant,
    IUserPermissionService permissions, IIdempotentCommandExecutor commands,
    IClinicalAccessAuditService audit, IConfiguration config) : IScheduleExceptionService
{
    private async Task RequireAsync(ICureFlowDbSession store, Guid facilityId, bool writing, bool clinical, CancellationToken ct)
    {
        if (!tenant.IsAuthenticated || tenant.IsPlatformUser || tenant.UserId is null || tenant.TenantId == Guid.Empty || tenant.ServiceIdentity is not null)
            throw new ForbiddenException();
        var actor = await store.GetByIdAsync<User>(tenant.UserId.Value, ct: ct) ?? throw new ForbiddenException();
        var codes = await permissions.GetPermissionCodesAsync(actor, ct, store);
        if (!actor.IsActive || !codes.Contains(writing ? "Staff.Edit" : "Staff.View", StringComparer.Ordinal)
            || (clinical && !codes.Contains("Patient.View", StringComparer.Ordinal))) throw new ForbiddenException();
        var facility = await store.QueryFirstOrDefaultAsync<CureFlow.Domain.Entities.Saas.Facility>("""
            SELECT * FROM "Facilities" WHERE "TenantId"=@TenantId AND "Id"=@facilityId AND NOT "IsDeleted"
            """, new { facilityId }, ct: ct) ?? throw new NotFoundException("Facility");
        if (writing && !facility.IsActive) throw new ConflictException("An active facility is required.");
        if (writing)
        {
            // Invisible appointments must never be treated as unaffected. This exposes no hidden subject or match.
            var hidden = await store.QueryFirstOrDefaultAsync<bool>("""
                SELECT EXISTS(SELECT 1 FROM "PatientConfidentialityStates" s WHERE s."TenantId"=@TenantId AND s."Restricted"
                 AND NOT public.cureflow_patient_read_allowed(s."TenantId",s."PatientId"))
                """, ct: ct);
            if (hidden) throw new ForbiddenException();
        }
    }

    public async Task<IReadOnlyList<ScheduleException>> ListAsync(Guid facilityId, Guid? afterId, int limit, CancellationToken ct)
    {
        await RequireAsync(db, facilityId, false, false, ct);
        var rows = await db.QueryAsync<ScheduleException>("""
            SELECT * FROM "ScheduleExceptions" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND NOT "IsDeleted"
              AND (CAST(@afterId AS uuid) IS NULL OR "Id">@afterId) ORDER BY "Id" LIMIT @limit
            """, new { facilityId, afterId, limit = Math.Clamp(limit, 1, 100) }, ct: ct);
        await audit.RecordAsync(new(null, "Facility", facilityId, "read_schedule_exceptions", "authorized", Purpose: "operations"), ct);
        return rows;
    }

    public async Task<ScheduleException> GetAsync(Guid facilityId, Guid exceptionId, CancellationToken ct)
    {
        await RequireAsync(db, facilityId, false, false, ct);
        var row = await db.QueryFirstOrDefaultAsync<ScheduleException>("""
            SELECT * FROM "ScheduleExceptions" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@exceptionId AND NOT "IsDeleted"
            """, new { facilityId, exceptionId }, ct: ct) ?? throw new NotFoundException("Schedule exception");
        await audit.RecordAsync(new(null, "Facility", facilityId, "read_schedule_exception", "authorized", row.Revision.ToString(), "operations"), ct);
        return row;
    }

    public async Task<IReadOnlyList<AppointmentRescheduleWork>> WorkAsync(Guid facilityId, Guid? afterId, int limit, CancellationToken ct)
    {
        var result = new List<AppointmentRescheduleWork>();
        await db.TransactionAsync(async store =>
        {
            await RequireAsync(store, facilityId, false, true, ct);
            var rows = await store.QueryAsync<AppointmentRescheduleWork>("""
                SELECT w.* FROM "AppointmentRescheduleWorks" w JOIN "ScheduleExceptions" e
                  ON e."TenantId"=w."TenantId" AND e."Id"=w."ExceptionId"
                WHERE w."TenantId"=@TenantId AND e."FacilityId"=@facilityId AND NOT w."IsDeleted"
                  AND (CAST(@afterId AS uuid) IS NULL OR w."Id">@afterId) ORDER BY w."Id" LIMIT @limit
                """, new { facilityId, afterId, limit = Math.Clamp(limit, 1, 100) }, ct: ct);
            foreach (var row in rows)
            {
                await audit.RecordAsync(new(row.PatientId, "Appointment", row.AppointmentId, "read_reschedule_work", "authorized", Purpose: "operations"), ct, store);
                result.Add(row);
            }
            if (rows.Count == 0) await audit.RecordAsync(new(null, "Facility", facilityId, "read_reschedule_work", "authorized", Purpose: "operations"), ct, store);
        }, ct);
        return result;
    }

    private TimeSpan Horizon()
    {
        var hours = config.GetValue<int?>("Scheduling:ApprovedRetryHorizonHours");
        if (hours is not > 0 or > 8760) throw new DomainException("Schedule retry policy is not configured.", 503);
        return TimeSpan.FromHours(hours.Value);
    }

    public Task<IdempotentCommandResult> CreateAsync(Guid facilityId, string key, ScheduleExceptionInput input, CancellationToken ct)
    {
        ScheduleExceptionRules.Validate(input);
        return commands.ExecuteAsync("schedule.exception.create", key, new { facilityId, input }, Horizon(),
            (store, token) => RequireAsync(store, facilityId, true, true, token), async (store, token) =>
            {
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)", ct: token);
                await RequireAsync(store, facilityId, true, true, token);
                if (await store.QueryFirstOrDefaultAsync<Guid?>("SELECT public.cureflow_lock_active_facility(@TenantId,@facilityId)", new { facilityId }, ct: token) is null)
                    throw new ConflictException("An active facility is required.");
                var settings = await store.QueryFirstOrDefaultAsync<FacilityOperatingSetting>("""
                    SELECT * FROM "FacilityOperatingSettings" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId ORDER BY "Revision" DESC LIMIT 1
                    """, new { facilityId }, ct: token);
                if (settings is null || settings.Timezone != input.Timezone) throw new ConflictException("Use the current facility timezone.");
                if (input.PractitionerUserId.HasValue && !await store.QueryFirstOrDefaultAsync<bool>("""
                    SELECT EXISTS(SELECT 1 FROM "Users" u JOIN "StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
                     WHERE u."TenantId"=@TenantId AND u."Id"=@id AND u."Role" IN (1,4,6) AND NOT u."IsDeleted" AND NOT p."IsDeleted")
                    """, new { id = input.PractitionerUserId.Value }, ct: token)) throw new NotFoundException("Practitioner");
                if (input.ResourceId.HasValue && !await store.QueryFirstOrDefaultAsync<bool>("""
                    SELECT EXISTS(SELECT 1 FROM "FacilityResources" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@id AND NOT "IsDeleted")
                    """, new { facilityId, id = input.ResourceId.Value }, ct: token)) throw new NotFoundException("Resource");
                var row = new ScheduleException { FacilityId = facilityId, PractitionerUserId = input.PractitionerUserId,
                    ResourceId = input.ResourceId, OperatingSettingId = settings.Id, Timezone = input.Timezone,
                    Kind = input.Kind, StartsAtUtc = input.StartsAtUtc, EndsAtUtc = input.EndsAtUtc, Reason = input.Reason.Trim() };
                IReadOnlyList<Appointment> appointments = await store.QueryAsync<Appointment>("""
                    SELECT "Id","PatientId","DoctorUserId","ScheduledAt","DurationMinutes","FacilityId","ReservationId" FROM "Appointments" WHERE "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted" AND "Status" IN (0,1)
                     AND ("FacilityId"=@facilityId OR ("FacilityId" IS NULL AND (CAST(@resource AS uuid) IS NULL OR CAST(@practitioner AS uuid) IS NOT NULL)))
                     AND (CAST(@practitioner AS uuid) IS NULL OR "DoctorUserId"=@practitioner)
                     AND (CAST(@resource AS uuid) IS NULL OR ("FacilityId" IS NULL AND CAST(@practitioner AS uuid) IS NOT NULL)
                       OR EXISTS(SELECT 1 FROM "AppointmentReservationSeats" s WHERE s."TenantId"=@TenantId
                        AND s."ReservationId"="Appointments"."ReservationId" AND s."TargetType"='resource' AND s."TargetId"=@resource AND s."State"='confirmed'))
                     AND "ScheduledAt"<@ends AND ("DurationMinutes" NOT BETWEEN 1 AND 1440
                        OR "ScheduledAt"+make_interval(mins=>"DurationMinutes")>@starts)
                    ORDER BY "Id" LIMIT 1001 FOR UPDATE
                    """, new { facilityId, resource = input.ResourceId, practitioner = input.PractitionerUserId, starts = input.StartsAtUtc, ends = input.EndsAtUtc }, ct: token);
                if (appointments.Count > 1000 || appointments.Any(a => a.DurationMinutes is < 1 or > 1440))
                    throw new ConflictException("Narrow this exception or resolve invalid existing appointment durations before reconciling bookings.");
                // Exact reserved resources are known; legacy venue/resource associations remain explicitly unverified.
                await store.InsertAsync(row, ct: token);
                foreach (var appointment in appointments)
                {
                    await store.InsertAsync(new AppointmentRescheduleWork { ExceptionId = row.Id, AppointmentId = appointment.Id,
                        PatientId = appointment.PatientId, OriginalScheduledAtUtc = appointment.ScheduledAt,
                        OriginalDurationMinutes = appointment.DurationMinutes, FacilityAssociationNeedsReview = !appointment.FacilityId.HasValue }, ct: token);
                    await audit.RecordAsync(new(appointment.PatientId, "Appointment", appointment.Id, "identify_exception_impact", "accepted", Purpose: "operations"), token, store);
                }
                await audit.RecordAsync(new(null, "Facility", facilityId, "create_schedule_exception", "accepted", row.Revision.ToString(), "operations"), token, store);
                return (new CommandReceipt(row.Id, row.Revision, CommandOutcome.Created), 201);
            }, ct);
    }

    public Task<IdempotentCommandResult> DisableAsync(Guid facilityId, Guid exceptionId, long expectedRevision, string key, CancellationToken ct)
    {
        if (expectedRevision < 1) throw new ValidationException("Supply the current exception version.");
        return commands.ExecuteAsync("schedule.exception.disable", key, new { facilityId, exceptionId, expectedRevision }, Horizon(),
            (store, token) => RequireAsync(store, facilityId, true, false, token), async (store, token) =>
            {
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)", ct: token);
                await RequireAsync(store, facilityId, true, false, token);
                var row = await store.QueryFirstOrDefaultAsync<ScheduleException>("""
                    SELECT * FROM "ScheduleExceptions" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@exceptionId FOR UPDATE
                    """, new { facilityId, exceptionId }, ct: token) ?? throw new NotFoundException("Schedule exception");
                if (row.Revision != expectedRevision) throw new DomainException("The exception changed. Reload before saving.", 412);
                if (!row.IsActive) throw new ConflictException("The exception is already disabled.");
                await store.ExecuteAsync("""
                    UPDATE "ScheduleExceptions" SET "IsActive"=false,"Revision"=@revision,"DisabledAt"=@now,"UpdatedAt"=@now,"UpdatedBy"=@actor
                     WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@exceptionId
                    """, new { revision = checked(row.Revision + 1), now = DateTime.UtcNow, actor = tenant.UserId, facilityId, exceptionId }, ct: token);
                // Potential impact remains visible; disabling never silently resolves, deletes or reschedules bookings.
                await audit.RecordAsync(new(null, "Facility", facilityId, "disable_schedule_exception", "accepted", (row.Revision + 1).ToString(), "operations"), token, store);
                return (new CommandReceipt(row.Id, row.Revision + 1, CommandOutcome.Updated), 200);
            }, ct);
    }
}

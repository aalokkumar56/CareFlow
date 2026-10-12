using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Validation;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Organization;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;

namespace CureFlow.Infrastructure.Services;

public sealed class ScheduleRuleService(ICureFlowDbSession db, ITenantContext tenant,
    IUserPermissionService permissions, IIdempotentCommandExecutor commands,
    IClinicalAccessAuditService audit, IConfiguration config) : IScheduleRuleService
{
    private async Task RequireAsync(ICureFlowDbSession store, Guid facilityId, bool writing, CancellationToken ct)
    {
        if (facilityId == Guid.Empty || !tenant.IsAuthenticated || tenant.IsPlatformUser || !tenant.UserId.HasValue || tenant.TenantId == Guid.Empty)
            throw new DomainException("Current hospital authority is required.", 403);
        var user = await store.GetByIdAsync<User>(tenant.UserId.Value, ct: ct);
        if (user is not { IsActive: true } || !(await permissions.GetPermissionCodesAsync(user, ct, store))
            .Contains(writing ? "Staff.Edit" : "Staff.View", StringComparer.Ordinal))
            throw new DomainException("Current schedule permission is required.", 403);
        var facility = await store.QueryFirstOrDefaultAsync<Facility>("""
            SELECT * FROM "Facilities" WHERE "TenantId"=@TenantId AND "Id"=@facilityId AND NOT "IsDeleted"
            """, new { facilityId }, ct: ct) ?? throw new NotFoundException("Facility");
        if (writing && !facility.IsActive) throw new DomainException("An active facility is required.", 409);
    }

    private static async Task<bool> EligibleAsync(ICureFlowDbSession store, Guid facilityId,
        Guid? practitionerUserId, Guid? resourceId, CancellationToken ct)
    {
        if (practitionerUserId.HasValue)
        {
            var user = await store.GetByIdAsync<User>(practitionerUserId.Value, ct: ct);
            if (user is not { IsActive: true } || user.Role is not (UserRole.Doctor or UserRole.Nurse or UserRole.Staff)) return false;
            var profile = await store.QueryFirstOrDefaultAsync<StaffProfile>("""
                SELECT * FROM "StaffProfiles" WHERE "TenantId"=@TenantId AND "UserId"=@userId AND "IsActive" AND NOT "IsDeleted"
                """, new { userId = practitionerUserId.Value }, ct: ct);
            if (profile is not { IsAvailable: true }) return false;
        }
        if (resourceId.HasValue)
        {
            var resource = await store.QueryFirstOrDefaultAsync<FacilityResource>("""
                SELECT * FROM "FacilityResources" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@id
                    AND "IsActive" AND NOT "IsDeleted" AND "OperationalStatus"='enabled'
                """, new { facilityId, id = resourceId.Value }, ct: ct);
            if (resource is null) return false;
        }
        return true;
    }

    public async Task<IReadOnlyList<ScheduleRule>> ListAsync(Guid facilityId, Guid? afterId, int limit, CancellationToken ct)
    {
        await RequireAsync(db, facilityId, false, ct);
        var rows = await db.QueryAsync<ScheduleRule>("""
            SELECT * FROM "ScheduleRules" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId
                AND NOT "IsDeleted" AND (CAST(@afterId AS uuid) IS NULL OR "Id">@afterId) ORDER BY "Id" LIMIT @limit
            """, new { facilityId, afterId, limit = Math.Clamp(limit, 1, 100) }, ct: ct);
        await audit.RecordAsync(new(null, "Facility", facilityId, "read_schedule_rules", "authorized", Purpose: "operations"), ct);
        return rows;
    }

    public async Task<ScheduleRule> GetAsync(Guid facilityId, Guid ruleId, CancellationToken ct)
    {
        await RequireAsync(db, facilityId, false, ct);
        var row = await db.QueryFirstOrDefaultAsync<ScheduleRule>("""
            SELECT * FROM "ScheduleRules" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@ruleId AND NOT "IsDeleted"
            """, new { facilityId, ruleId }, ct: ct) ?? throw new NotFoundException("Schedule rule");
        await audit.RecordAsync(new(null, "Facility", facilityId, "read_schedule_rule", "authorized", row.Revision.ToString(), "operations"), ct);
        return row;
    }

    public async Task<IReadOnlyList<ScheduleAvailabilityWindow>> AvailabilityAsync(Guid facilityId, DateOnly from,
        DateOnly through, Guid? practitionerUserId, Guid? resourceId, CancellationToken ct)
    {
        ScheduleRuleExpansion.ValidateHorizon(from, through);
        if (!practitionerUserId.HasValue && !resourceId.HasValue)
            throw new ValidationException("Select a practitioner or resource for availability.");
        var windows = new List<ScheduleAvailabilityWindow>();
        await db.TransactionAsync(async store =>
        {
            await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)", ct: ct);
            await RequireAsync(store, facilityId, false, ct);
            await audit.RecordAsync(new(null, "Facility", facilityId, "read_schedule_availability", "authorized", Purpose: "operations"), ct, store);
            var facility = await store.GetByIdAsync<Facility>(facilityId, ct: ct);
            if (facility is not { IsActive: true }) return;
            if (!await EligibleAsync(store, facilityId, practitionerUserId, resourceId, ct)) return;
            var rows = await store.QueryAsync<ScheduleRule>("""
                SELECT * FROM "ScheduleRules" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "IsActive" AND NOT "IsDeleted"
                  AND "EffectiveFrom"<=@through AND "EffectiveThrough">=@from
                  AND (CAST(@practitionerUserId AS uuid) IS NULL OR "PractitionerUserId"=@practitionerUserId)
                  AND (CAST(@resourceId AS uuid) IS NULL OR "ResourceId"=@resourceId)
                ORDER BY "Id" LIMIT 201
                """, new { facilityId, from, through, practitionerUserId, resourceId }, ct: ct);
            if (rows.Count > 200) throw new DomainException("Narrow this availability query.", 409);
            foreach (var row in rows)
            {
                if (!await EligibleAsync(store, facilityId, row.PractitionerUserId, row.ResourceId, ct)) continue;
                var expanded = ScheduleRuleExpansion.Expand(row, from, through);
                if (expanded.Count > 0)
                {
                    var exceptions = await store.QueryAsync<ScheduleException>("""
                        SELECT * FROM "ScheduleExceptions" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "IsActive" AND NOT "IsDeleted"
                         AND "StartsAtUtc"<@last AND "EndsAtUtc">@first
                         AND ("PractitionerUserId" IS NULL OR "PractitionerUserId"=@practitioner)
                         AND ("ResourceId" IS NULL OR "ResourceId"=@resource) ORDER BY "StartsAtUtc","Id" LIMIT 201
                        """, new { facilityId, first = expanded.Min(w=>w.StartsAtUtc), last = expanded.Max(w=>w.EndsAtUtc),
                            practitioner = row.PractitionerUserId, resource = row.ResourceId }, ct: ct);
                    if (exceptions.Count > 200) throw new DomainException("Narrow this availability query.",409);
                    expanded = ScheduleExceptionRules.Apply(expanded,row,exceptions);
                }
                if (row.ResourceId.HasValue)
                {
                    var resource = await store.GetByIdAsync<FacilityResource>(row.ResourceId.Value, ct: ct);
                    expanded = expanded.Where(w => resource is not null && w.StartsAtUtc >= resource.EffectiveFrom
                        && (!resource.EffectiveUntil.HasValue || w.EndsAtUtc <= resource.EffectiveUntil.Value)).ToArray();
                }
                windows.AddRange(expanded);
                if (windows.Count > 5000) throw new DomainException("Narrow this availability query.",409);
            }
        }, ct);
        return windows.OrderBy(w => w.StartsAtUtc).ThenBy(w => w.RuleId).ToArray();
    }

    private TimeSpan Horizon()
    {
        var hours = config.GetValue<int?>("Scheduling:ApprovedRetryHorizonHours");
        if (hours is not > 0 or > 8760) throw new DomainException("Schedule retry policy is not configured.", 503);
        return TimeSpan.FromHours(hours.Value);
    }

    public Task<IdempotentCommandResult> CreateAsync(Guid facilityId, string key, ScheduleRuleInput input, CancellationToken ct)
    {
        ScheduleRuleExpansion.Validate(input);
        var probe = new ScheduleRule { FacilityId = facilityId, PractitionerUserId = input.PractitionerUserId,
            ResourceId = input.ResourceId, Timezone = input.Timezone, EffectiveFrom = input.EffectiveFrom,
            EffectiveThrough = input.EffectiveThrough, WeekdayMask = input.WeekdayMask, StartMinute = input.StartMinute,
            EndMinute = input.EndMinute, AmbiguousTimePolicy = input.AmbiguousTimePolicy };
        // Validate DST decisions over the entire bounded effective interval before committing a rule.
        for (var day = input.EffectiveFrom.DayNumber; day <= input.EffectiveThrough.DayNumber; day += 31)
            ScheduleRuleExpansion.Expand(probe, DateOnly.FromDayNumber(day), DateOnly.FromDayNumber(Math.Min(day + 30, input.EffectiveThrough.DayNumber)));
        return commands.ExecuteAsync("schedule.rule.create", key, new { facilityId, input }, Horizon(),
            (store, token) => RequireAsync(store, facilityId, true, token), async (store, token) =>
            {
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)", ct: token);
                await RequireAsync(store, facilityId, true, token);
                if (!await EligibleAsync(store, facilityId, input.PractitionerUserId, input.ResourceId, token))
                    throw new DomainException("The selected practitioner or resource is unavailable.", 409);
                var settings = await store.QueryFirstOrDefaultAsync<FacilityOperatingSetting>("""
                    SELECT * FROM "FacilityOperatingSettings" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId
                        ORDER BY "Revision" DESC LIMIT 1
                    """, new { facilityId }, ct: token);
                if (settings is null || settings.Timezone != input.Timezone)
                    throw new DomainException("Use the current explicit facility timezone.", 409);
                probe.OperatingSettingId = settings.Id;
                await store.InsertAsync(probe, ct: token);
                await audit.RecordAsync(new(null, "Facility", facilityId, "create_schedule_rule", "accepted", probe.Revision.ToString(), "operations"), token, store);
                return (new CommandReceipt(probe.Id, probe.Revision, CommandOutcome.Created), 201);
            }, ct);
    }

    public Task<IdempotentCommandResult> DisableAsync(Guid facilityId, Guid ruleId, long expectedRevision, string key, CancellationToken ct)
    {
        if (expectedRevision < 1) throw new ValidationException("Supply the current schedule version.");
        return commands.ExecuteAsync("schedule.rule.disable", key, new { facilityId, ruleId, expectedRevision }, Horizon(),
            (store, token) => RequireAsync(store, facilityId, true, token), async (store, token) =>
            {
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)", ct: token);
                await RequireAsync(store, facilityId, true, token);
                var row = await store.QueryFirstOrDefaultAsync<ScheduleRule>("""
                    SELECT * FROM "ScheduleRules" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@ruleId
                        AND NOT "IsDeleted" FOR UPDATE
                    """, new { facilityId, ruleId }, ct: token) ?? throw new NotFoundException("Schedule rule");
                if (row.Revision != expectedRevision) throw new DomainException("The schedule changed. Reload before saving.", 412);
                if (!row.IsActive) throw new DomainException("The schedule is already disabled.", 409);
                row.IsActive = false;
                row.Revision = checked(row.Revision + 1);
                await store.ExecuteAsync("""
                    UPDATE "ScheduleRules" SET "IsActive"=false,"Revision"=@revision,"UpdatedAt"=@now,"UpdatedBy"=@actor
                     WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@ruleId
                    """, new { revision = row.Revision, now = DateTime.UtcNow, actor = tenant.UserId, facilityId, ruleId }, ct: token);
                await audit.RecordAsync(new(null, "Facility", facilityId, "disable_schedule_rule", "accepted", row.Revision.ToString(), "operations"), token, store);
                return (new CommandReceipt(row.Id, row.Revision, CommandOutcome.Updated), 200);
            }, ct);
    }
}

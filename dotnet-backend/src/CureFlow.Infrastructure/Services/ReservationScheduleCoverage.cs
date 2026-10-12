using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Validation;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Organization;
namespace CureFlow.Infrastructure.Services;
internal static class ReservationScheduleCoverage
{
    public static async Task RequireAsync(ICureFlowDbSession store,Guid facilityId,Guid practitionerId,Guid[] resourceIds,DateTime starts,DateTime ends,CancellationToken ct)
    {
        var setting=await store.QueryFirstOrDefaultAsync<FacilityOperatingSetting>("""
            SELECT * FROM "FacilityOperatingSettings" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId
              AND "EffectiveFrom"<=CURRENT_TIMESTAMP ORDER BY "EffectiveFrom" DESC,"Revision" DESC LIMIT 1
            """,new{facilityId},ct:ct)??throw new ConflictException("Configure current facility operating settings before reservation.");
        var from=DateOnly.FromDateTime(starts.AddDays(-2));var through=DateOnly.FromDateTime(ends.AddDays(2));
        var rules=await store.QueryAsync<ScheduleRule>("""
            SELECT * FROM "ScheduleRules" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "OperatingSettingId"=@settingId
              AND "IsActive" AND NOT "IsDeleted" AND "EffectiveFrom"<=@through AND "EffectiveThrough">=@from
              AND (("PractitionerUserId"=@practitionerId AND ("ResourceId" IS NULL OR "ResourceId"=ANY(@resourceIds)))
                OR ("ResourceId"=ANY(@resourceIds) AND ("PractitionerUserId" IS NULL OR "PractitionerUserId"=@practitionerId)))
            ORDER BY "Id" LIMIT 1001
            """,new{facilityId,settingId=setting.Id,from,through,practitionerId,resourceIds},ct:ct);
        if(rules.Count>1000)throw new ConflictException("Schedule rule scope exceeds the reservation bound.");
        bool Covered(IEnumerable<ScheduleRule> matching)
        {
            var covered=starts;
            foreach(var window in matching.SelectMany(r=>ScheduleRuleExpansion.Expand(r,from,through)).OrderBy(w=>w.StartsAtUtc))
            {
                if(window.EndsAtUtc<=covered)continue;
                if(window.StartsAtUtc>covered)break;
                if(window.EndsAtUtc>covered)covered=window.EndsAtUtc;
                if(covered>=ends)return true;
            }
            return false;
        }
        if(!Covered(rules.Where(r=>r.PractitionerUserId==practitionerId)))throw new ConflictException("Practitioner interval is outside current explicit availability.");
        foreach(var resource in resourceIds)
            if(!Covered(rules.Where(r=>r.ResourceId==resource)))throw new ConflictException("Resource interval is outside current explicit availability.");
        if(await store.QueryFirstOrDefaultAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "ScheduleExceptions" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId
              AND "IsActive" AND NOT "IsDeleted" AND "DisabledAt" IS NULL AND "StartsAtUtc"<@ends AND "EndsAtUtc">@starts
              AND ("PractitionerUserId" IS NULL OR "PractitionerUserId"=@practitionerId)
              AND ("ResourceId" IS NULL OR "ResourceId"=ANY(@resourceIds)))
            """,new{facilityId,practitionerId,resourceIds,starts,ends},ct:ct))
            throw new ConflictException("Selected interval is blocked by a retained schedule exception.");
    }
}
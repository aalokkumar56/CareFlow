using CureFlow.Domain.Entities;
namespace CureFlow.Application.Interfaces;

public record ScheduleRuleInput(Guid? PractitionerUserId, Guid? ResourceId, string Timezone,
    DateOnly EffectiveFrom, DateOnly EffectiveThrough, int WeekdayMask, int StartMinute, int EndMinute,
    string AmbiguousTimePolicy);
public record ScheduleAvailabilityWindow(Guid RuleId, DateOnly LocalDate, DateTime StartsAtUtc, DateTime EndsAtUtc, string Timezone);
public interface IScheduleRuleService
{
    Task<IReadOnlyList<ScheduleRule>> ListAsync(Guid facilityId, Guid? afterId, int limit, CancellationToken ct);
    Task<ScheduleRule> GetAsync(Guid facilityId, Guid ruleId, CancellationToken ct);
    Task<IReadOnlyList<ScheduleAvailabilityWindow>> AvailabilityAsync(Guid facilityId, DateOnly from, DateOnly through, Guid? practitionerUserId, Guid? resourceId, CancellationToken ct);
    Task<IdempotentCommandResult> CreateAsync(Guid facilityId, string key, ScheduleRuleInput input, CancellationToken ct);
    Task<IdempotentCommandResult> DisableAsync(Guid facilityId, Guid ruleId, long expectedRevision, string key, CancellationToken ct);
}

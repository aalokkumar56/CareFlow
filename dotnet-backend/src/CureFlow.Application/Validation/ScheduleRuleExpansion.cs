using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;

namespace CureFlow.Application.Validation;

public static class ScheduleRuleExpansion
{
    public static TimeZoneInfo Validate(ScheduleRuleInput input)
    {
        if ((!input.PractitionerUserId.HasValue && !input.ResourceId.HasValue)
            || input.PractitionerUserId == Guid.Empty || input.ResourceId == Guid.Empty)
            throw new ValidationException("Select a practitioner or resource.");
        if (input.EffectiveFrom == default || input.EffectiveThrough < input.EffectiveFrom
            || input.EffectiveThrough.DayNumber - input.EffectiveFrom.DayNumber > 365)
            throw new ValidationException("Supply an effective date range of at most 366 days.");
        if (input.WeekdayMask is < 1 or > 127 || input.StartMinute is < 0 or > 1439
            || input.EndMinute <= input.StartMinute || input.EndMinute > 1440)
            throw new ValidationException("Supply selected weekdays and a same-day time interval.");
        if (input.AmbiguousTimePolicy is not ("reject" or "earlier" or "later"))
            throw new ValidationException("Select reject, earlier or later for ambiguous local times.");
        if (string.IsNullOrWhiteSpace(input.Timezone) || input.Timezone.Length > 100 || input.Timezone != input.Timezone.Trim())
            throw new ValidationException("Supply the facility's IANA timezone.");
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(input.Timezone);
            if (!zone.HasIanaId) throw new ValidationException("Supply an IANA timezone.");
            // Reject a rule that never occurs within its effective interval.
            if (!Enumerable.Range(0, Math.Min(7, input.EffectiveThrough.DayNumber - input.EffectiveFrom.DayNumber + 1))
                .Any(day => (input.WeekdayMask & (1 << (int)input.EffectiveFrom.AddDays(day).DayOfWeek)) != 0))
                throw new ValidationException("The selected weekdays do not occur in the effective date range.");
            return zone;
        }
        catch (TimeZoneNotFoundException) { throw new ValidationException("Supply a recognized IANA timezone."); }
        catch (InvalidTimeZoneException) { throw new ValidationException("Supply a valid IANA timezone."); }
    }

    public static void ValidateHorizon(DateOnly from, DateOnly through)
    {
        if (from == default || through < from || through.DayNumber - from.DayNumber > 30)
            throw new ValidationException("Availability queries must span at most 31 days.");
    }

    public static IReadOnlyList<ScheduleAvailabilityWindow> Expand(ScheduleRule rule, DateOnly from, DateOnly through)
    {
        ValidateHorizon(from, through);
        var zone = Validate(new(rule.PractitionerUserId, rule.ResourceId, rule.Timezone, rule.EffectiveFrom,
            rule.EffectiveThrough, rule.WeekdayMask, rule.StartMinute, rule.EndMinute, rule.AmbiguousTimePolicy));
        if (!rule.IsActive || rule.IsDeleted) return [];
        var first = Math.Max(from.DayNumber, rule.EffectiveFrom.DayNumber);
        var last = Math.Min(through.DayNumber, rule.EffectiveThrough.DayNumber);
        var result = new List<ScheduleAvailabilityWindow>();
        for (var day = first; day <= last; day++)
        {
            var date = DateOnly.FromDayNumber(day);
            if ((rule.WeekdayMask & (1 << (int)date.DayOfWeek)) == 0) continue;
            var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            // 24:00 on the final supported calendar date cannot be represented.
            if (date == DateOnly.MaxValue && rule.EndMinute == 1440)
                throw new ValidationException("The interval exceeds the supported calendar.");
            var start = Resolve(midnight.AddMinutes(rule.StartMinute), zone, rule.AmbiguousTimePolicy);
            var end = Resolve(midnight.AddMinutes(rule.EndMinute), zone, rule.AmbiguousTimePolicy);
            if (end <= start) throw new DomainException("This rule has a non-positive interval after timezone conversion.", 409);
            result.Add(new(rule.Id, date, start, end, rule.Timezone));
        }
        return result;
    }

    private static DateTime Resolve(DateTime local, TimeZoneInfo zone, string policy)
    {
        if (zone.IsInvalidTime(local))
            throw new DomainException("This rule crosses a nonexistent local time. Revise the affected rule.", 409);
        if (!zone.IsAmbiguousTime(local)) return TimeZoneInfo.ConvertTimeToUtc(local, zone);
        if (policy == "reject") throw new DomainException("This rule encounters an ambiguous local time. Select an explicit offset policy.", 409);
        var offsets = zone.GetAmbiguousTimeOffsets(local);
        return new DateTimeOffset(local, policy == "earlier" ? offsets.Max() : offsets.Min()).UtcDateTime;
    }
}

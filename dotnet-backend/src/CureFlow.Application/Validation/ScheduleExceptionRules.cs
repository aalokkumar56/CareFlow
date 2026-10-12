using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;

namespace CureFlow.Application.Validation;

public static class ScheduleExceptionRules
{
    public static void Validate(ScheduleExceptionInput input)
    {
        if (input.PractitionerUserId == Guid.Empty || input.ResourceId == Guid.Empty
            || input.Kind is not ("leave" or "holiday" or "block")
            || (input.Kind == "leave" && !input.PractitionerUserId.HasValue)
            || (input.Kind == "holiday" && (input.PractitionerUserId.HasValue || input.ResourceId.HasValue)))
            throw new ValidationException("Supply an explicit leave, facility holiday or availability block target.");
        if (input.StartsAtUtc == default || input.StartsAtUtc.Kind != DateTimeKind.Utc || input.EndsAtUtc.Kind != DateTimeKind.Utc
            || input.EndsAtUtc <= input.StartsAtUtc || input.EndsAtUtc - input.StartsAtUtc > TimeSpan.FromDays(31))
            throw new ValidationException("Supply explicit UTC endpoints for an interval of at most 31 days.");
        if (string.IsNullOrWhiteSpace(input.Timezone) || input.Timezone.Length > 100 || input.Timezone != input.Timezone.Trim())
            throw new ValidationException("Supply the current facility IANA timezone.");
        try
        {
            if (!TimeZoneInfo.FindSystemTimeZoneById(input.Timezone).HasIanaId)
                throw new ValidationException("Supply an IANA timezone.");
        }
        catch (TimeZoneNotFoundException) { throw new ValidationException("Supply a recognized IANA timezone."); }
        catch (InvalidTimeZoneException) { throw new ValidationException("Supply a valid IANA timezone."); }
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 3 || input.Reason.Length > 500 || input.Reason.Any(char.IsControl))
            throw new ValidationException("Supply a bounded operational reason without clinical details.");
    }

    public static IReadOnlyList<ScheduleAvailabilityWindow> Apply(IReadOnlyList<ScheduleAvailabilityWindow> windows,
        ScheduleRule rule, IReadOnlyList<ScheduleException> exceptions)
    {
        var result = new List<ScheduleAvailabilityWindow>();
        foreach (var window in windows)
        {
            var parts = new List<(DateTime Start, DateTime End)> { (window.StartsAtUtc, window.EndsAtUtc) };
            foreach (var exception in exceptions.Where(e => e.IsActive && !e.IsDeleted && e.FacilityId == rule.FacilityId
                && (!e.PractitionerUserId.HasValue || e.PractitionerUserId == rule.PractitionerUserId)
                && (!e.ResourceId.HasValue || e.ResourceId == rule.ResourceId)).OrderBy(e => e.StartsAtUtc))
            {
                var next = new List<(DateTime Start, DateTime End)>();
                foreach (var part in parts)
                {
                    if (exception.EndsAtUtc <= part.Start || exception.StartsAtUtc >= part.End) { next.Add(part); continue; }
                    if (exception.StartsAtUtc > part.Start) next.Add((part.Start, exception.StartsAtUtc));
                    if (exception.EndsAtUtc < part.End) next.Add((exception.EndsAtUtc, part.End));
                }
                parts = next;
            }
            result.AddRange(parts.Select(p => window with { StartsAtUtc = p.Start, EndsAtUtc = p.End }));
            if (result.Count > 5000) throw new DomainException("Narrow the availability query.", 409);
        }
        return result;
    }
}

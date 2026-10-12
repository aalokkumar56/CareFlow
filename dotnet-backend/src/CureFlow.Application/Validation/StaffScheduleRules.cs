using CureFlow.Application.Common;

namespace CureFlow.Application.Validation;

public static class StaffScheduleRules
{
    public static void Validate(int? day, DateTime? date, TimeSpan start, TimeSpan end, string? notes)
    {
        if (day is < 0 or > 6 || day.HasValue == date.HasValue)
            throw new ValidationException("Select a weekday from 0 to 6 or one specific date.");
        if (start < TimeSpan.Zero || end > TimeSpan.FromDays(1) || end <= start)
            throw new ValidationException("Schedule times must define a positive interval within one day.");
        if (notes is not null && (notes.Length > 2000 || notes.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t')))
            throw new ValidationException("Schedule notes exceed their permitted format or length.");
    }
}

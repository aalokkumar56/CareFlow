namespace CureFlow.Application.Common;

public static class DateTimeHelper
{
    /// <summary>
    /// Ensures a DateTime is UTC for PostgreSQL timestamptz columns.
    /// Unspecified values are treated as UTC (common from JSON/API input).
    /// </summary>
    public static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    public static DateTime? EnsureUtc(DateTime? value) =>
        value.HasValue ? EnsureUtc(value.Value) : null;
}

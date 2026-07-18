using System.Globalization;

namespace CureFlow.Application.Common;

/// <summary>
/// Converts between UTC instants and hospital wall-clock using IANA tenant timezones.
/// </summary>
public static class TenantTimeHelper
{
    public const string DefaultTimeZoneId = "Asia/Kolkata";

    public static string NormalizeTimeZoneId(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();

    /// <summary>Validates an IANA id for settings updates. Empty becomes the default.</summary>
    public static string RequireValidTimeZoneId(string? timeZoneId)
    {
        var id = NormalizeTimeZoneId(timeZoneId);
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return id;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ValidationException($"Unknown timezone: {id}");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ValidationException($"Unknown timezone: {id}");
        }
    }

    public static bool TryResolveTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        var id = NormalizeTimeZoneId(timeZoneId);
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
            return false;
        }
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        TryResolveTimeZone(timeZoneId, out var tz);
        return tz;
    }

    /// <summary>Converts a UTC instant to hospital-local wall clock (Unspecified kind).</summary>
    public static DateTime UtcToHospitalLocal(DateTime utc, string? timeZoneId)
    {
        var utcDt = DateTimeHelper.EnsureUtc(utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcDt, ResolveTimeZone(timeZoneId));
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    /// <summary>Interprets a hospital-local wall clock as UTC.</summary>
    public static DateTime HospitalLocalToUtc(DateTime hospitalLocal, string? timeZoneId)
    {
        var unspecified = DateTime.SpecifyKind(hospitalLocal, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, ResolveTimeZone(timeZoneId));
    }

    /// <summary>UTC half-open range [start, end) covering one hospital calendar day.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) HospitalDayRangeUtc(DateTime hospitalDate, string? timeZoneId)
    {
        var day = DateTime.SpecifyKind(hospitalDate.Date, DateTimeKind.Unspecified);
        return (HospitalLocalToUtc(day, timeZoneId), HospitalLocalToUtc(day.AddDays(1), timeZoneId));
    }

    /// <summary>UTC half-open range for the hospital's current calendar day.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) HospitalTodayRangeUtc(string? timeZoneId)
    {
        var hospitalNow = UtcToHospitalLocal(DateTime.UtcNow, timeZoneId);
        return HospitalDayRangeUtc(hospitalNow, timeZoneId);
    }

    public static string FormatHospitalDate(DateTime utc, string? timeZoneId) =>
        UtcToHospitalLocal(utc, timeZoneId).ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string FormatHospitalTime12h(DateTime utc, string? timeZoneId) =>
        UtcToHospitalLocal(utc, timeZoneId).ToString("hh:mm tt", CultureInfo.InvariantCulture);

    /// <summary>Short zone label for patient messages (e.g. IST, EDT). Falls back to UTC offset.</summary>
    public static string FormatTimezoneAbbr(DateTime utc, string? timeZoneId)
    {
        var id = NormalizeTimeZoneId(timeZoneId);
        var tz = ResolveTimeZone(timeZoneId);
        var utcDt = DateTimeHelper.EnsureUtc(utc);
        var isDst = tz.IsDaylightSavingTime(utcDt);

        return id switch
        {
            "Asia/Kolkata" => "IST",
            "Asia/Dubai" => "GST",
            "Asia/Singapore" => "SGT",
            "Europe/London" => isDst ? "BST" : "GMT",
            "Europe/Paris" => isDst ? "CEST" : "CET",
            "America/New_York" => isDst ? "EDT" : "EST",
            "America/Chicago" => isDst ? "CDT" : "CST",
            "America/Denver" => isDst ? "MDT" : "MST",
            "America/Los_Angeles" => isDst ? "PDT" : "PST",
            "America/Toronto" => isDst ? "EDT" : "EST",
            "America/Vancouver" => isDst ? "PDT" : "PST",
            "Australia/Sydney" => isDst ? "AEDT" : "AEST",
            _ => FormatUtcOffsetLabel(tz.GetUtcOffset(utcDt)),
        };
    }

    public static string FormatHospitalTime12hWithZone(DateTime utc, string? timeZoneId) =>
        $"{FormatHospitalTime12h(utc, timeZoneId)} {FormatTimezoneAbbr(utc, timeZoneId)}";

    private static string FormatUtcOffsetLabel(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return abs.Minutes == 0
            ? $"UTC{sign}{abs.Hours}"
            : $"UTC{sign}{abs.Hours}:{abs.Minutes:D2}";
    }
}

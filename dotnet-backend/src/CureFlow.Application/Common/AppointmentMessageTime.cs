namespace CureFlow.Application.Common;

/// <summary>
/// Formats appointment instants for patient messages (WhatsApp/email).
/// Hospital timezone is primary today; <paramref name="patientTimeZoneId"/> is reserved for later dual-local messages.
/// </summary>
public static class AppointmentMessageTime
{
    public sealed record Parts(
        string Date,
        string Time,
        string TimeWithZone,
        string TimezoneAbbr,
        string? PatientTime = null,
        string? PatientTimeWithZone = null);

    /// <param name="patientTimeZoneId">Unused for now (always null). Reserved for patient-local WhatsApp later.</param>
    public static Parts Format(DateTime utcScheduledAt, string? hospitalTimeZoneId, string? patientTimeZoneId = null)
    {
        var date = TenantTimeHelper.FormatHospitalDate(utcScheduledAt, hospitalTimeZoneId);
        var time = TenantTimeHelper.FormatHospitalTime12h(utcScheduledAt, hospitalTimeZoneId);
        var abbr = TenantTimeHelper.FormatTimezoneAbbr(utcScheduledAt, hospitalTimeZoneId);
        var timeWithZone = $"{time} {abbr}";

        string? patientTime = null;
        string? patientTimeWithZone = null;
        if (!string.IsNullOrWhiteSpace(patientTimeZoneId))
        {
            patientTime = TenantTimeHelper.FormatHospitalTime12h(utcScheduledAt, patientTimeZoneId);
            var patientAbbr = TenantTimeHelper.FormatTimezoneAbbr(utcScheduledAt, patientTimeZoneId);
            patientTimeWithZone = $"{patientTime} {patientAbbr}";
        }

        return new Parts(date, time, timeWithZone, abbr, patientTime, patientTimeWithZone);
    }

    public static Dictionary<string, string?> ToTemplateValues(Parts parts) => new()
    {
        ["date"] = parts.Date,
        ["time"] = parts.Time,
        ["time_with_zone"] = parts.TimeWithZone,
        ["timezone_abbr"] = parts.TimezoneAbbr,
        ["time_patient"] = parts.PatientTime,
        ["time_patient_with_zone"] = parts.PatientTimeWithZone,
    };
}

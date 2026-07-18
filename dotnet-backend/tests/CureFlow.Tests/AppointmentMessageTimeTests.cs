using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.Tests;

public class AppointmentMessageTimeTests
{
    [Fact]
    public void Format_hospital_only_includes_zone_label_and_reserved_patient_keys()
    {
        var utc = new DateTime(2026, 7, 18, 5, 30, 0, DateTimeKind.Utc);
        var parts = AppointmentMessageTime.Format(utc, "Asia/Kolkata", patientTimeZoneId: null);

        parts.Date.Should().Be("18 Jul 2026");
        parts.Time.Should().Be("11:00 AM");
        parts.TimeWithZone.Should().Be("11:00 AM IST");
        parts.TimezoneAbbr.Should().Be("IST");
        parts.PatientTime.Should().BeNull();
        parts.PatientTimeWithZone.Should().BeNull();

        var values = AppointmentMessageTime.ToTemplateValues(parts);
        values["time_with_zone"].Should().Be("11:00 AM IST");
        values["time"].Should().Be("11:00 AM");
        values.Should().ContainKey("time_patient");
    }

    [Fact]
    public void Format_with_patient_timezone_fills_dual_local_placeholders()
    {
        var utc = new DateTime(2026, 7, 18, 5, 30, 0, DateTimeKind.Utc);
        var parts = AppointmentMessageTime.Format(utc, "Asia/Kolkata", "America/New_York");

        parts.TimeWithZone.Should().Be("11:00 AM IST");
        parts.PatientTime.Should().Be("01:30 AM");
        parts.PatientTimeWithZone.Should().Be("01:30 AM EDT");
    }

    [Fact]
    public void Format_AmericaNewYork_summer_uses_EDT_label()
    {
        // 15:00 UTC = 11:00 AM EDT on 2026-07-18
        var utc = new DateTime(2026, 7, 18, 15, 0, 0, DateTimeKind.Utc);
        var parts = AppointmentMessageTime.Format(utc, "America/New_York");

        parts.Time.Should().Be("11:00 AM");
        parts.TimeWithZone.Should().Be("11:00 AM EDT");
    }
}

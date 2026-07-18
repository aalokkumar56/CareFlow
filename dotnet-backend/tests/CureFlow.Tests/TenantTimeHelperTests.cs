using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.Tests;

public class TenantTimeHelperTests
{
    [Fact]
    public void HospitalLocalToUtc_AsiaKolkata_11am_is_0530Z()
    {
        var local = new DateTime(2026, 7, 18, 11, 0, 0, DateTimeKind.Unspecified);
        var utc = TenantTimeHelper.HospitalLocalToUtc(local, "Asia/Kolkata");

        utc.Kind.Should().Be(DateTimeKind.Utc);
        utc.Should().Be(new DateTime(2026, 7, 18, 5, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void FormatHospitalTime12h_0530Z_is_11am_in_Kolkata()
    {
        var utc = new DateTime(2026, 7, 18, 5, 30, 0, DateTimeKind.Utc);

        TenantTimeHelper.FormatHospitalTime12h(utc, "Asia/Kolkata").Should().Be("11:00 AM");
        TenantTimeHelper.FormatHospitalDate(utc, "Asia/Kolkata").Should().Be("18 Jul 2026");
    }

    [Fact]
    public void FormatHospitalTime12h_ignores_null_timezone_and_defaults_to_Kolkata()
    {
        var utc = new DateTime(2026, 7, 18, 5, 30, 0, DateTimeKind.Utc);
        TenantTimeHelper.FormatHospitalTime12h(utc, null).Should().Be("11:00 AM");
    }

    [Fact]
    public void RoundTrip_hospital_local_preserves_wall_clock()
    {
        var local = new DateTime(2026, 12, 1, 15, 45, 0, DateTimeKind.Unspecified);
        var utc = TenantTimeHelper.HospitalLocalToUtc(local, "Asia/Kolkata");
        var back = TenantTimeHelper.UtcToHospitalLocal(utc, "Asia/Kolkata");

        back.Should().Be(local);
    }

    [Fact]
    public void FormatTimezoneAbbr_Kolkata_is_IST()
    {
        var utc = new DateTime(2026, 7, 18, 5, 30, 0, DateTimeKind.Utc);
        TenantTimeHelper.FormatTimezoneAbbr(utc, "Asia/Kolkata").Should().Be("IST");
        TenantTimeHelper.FormatHospitalTime12hWithZone(utc, "Asia/Kolkata").Should().Be("11:00 AM IST");
    }

    [Fact]
    public void RequireValidTimeZoneId_accepts_NewYork_and_rejects_garbage()
    {
        TenantTimeHelper.RequireValidTimeZoneId("America/New_York").Should().Be("America/New_York");
        TenantTimeHelper.RequireValidTimeZoneId(null).Should().Be("Asia/Kolkata");
        var act = () => TenantTimeHelper.RequireValidTimeZoneId("Not/A_Real_Zone");
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void HospitalDayRangeUtc_Kolkata_spans_previous_utc_evening()
    {
        var day = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Unspecified);
        var (start, end) = TenantTimeHelper.HospitalDayRangeUtc(day, "Asia/Kolkata");

        start.Should().Be(new DateTime(2026, 7, 17, 18, 30, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 7, 18, 18, 30, 0, DateTimeKind.Utc));
    }
}

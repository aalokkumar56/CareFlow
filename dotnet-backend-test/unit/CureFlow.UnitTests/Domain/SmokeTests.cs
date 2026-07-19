using CureFlow.Application.DTOs;
using CureFlow.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests.Domain;

public class SmokeTests
{
    [Fact]
    public void EnumsAreDefined()
    {
        Enum.GetValues<UserRole>().Should().Contain(UserRole.Doctor);
        Enum.GetValues<AllergySeverity>().Should().Contain(AllergySeverity.LifeThreatening);
        Enum.GetValues<PrescriptionRouteType>().Should().Contain(PrescriptionRouteType.Iv);
        Enum.GetValues<SmokingStatus>().Should().Contain(SmokingStatus.Current);
    }

    [Fact]
    public void AuthResponse_RecordEquality()
    {
        var u = new UserDto(Guid.Empty, "n", "e", UserRole.Admin, true, null, null);
        var t = new TenantDto(
            Guid.Empty, "s", "n", SubscriptionPlan.Trial, SubscriptionStatus.Trialing,
            TenantLifecycleStatus.Active, null, DateTime.UtcNow, true, "Asia/Kolkata", DateTime.UtcNow);
        var a = new AuthResponse("tok", u, t);
        a.AccessToken.Should().Be("tok");
    }
}

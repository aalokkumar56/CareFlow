using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CureFlow.Application.Common;
using CureFlow.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(
        string? secret = "unit-test-secret-key-min-32-chars!!",
        string issuer = "cureflow-test",
        string audience = "cureflow-api-test")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = secret,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
            })
            .Build();
        return new JwtTokenService(config);
    }

    [Fact]
    public void Issue_includes_tenant_user_role_and_permission_claims()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var jwt = CreateService().Issue(
            userId,
            tenantId,
            "doc@hospital.test",
            "Doctor",
            new[] { CureFlowPermissions.PatientView, CureFlowPermissions.AppointmentCreate, "Patient.View" });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        token.Issuer.Should().Be("cureflow-test");
        token.Audiences.Should().Contain("cureflow-api-test");
        token.Subject.Should().Be(userId.ToString());
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "doc@hospital.test");
        token.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
        token.Claims.Should().Contain(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == "Doctor");
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);

        var permissions = token.Claims
            .Where(c => c.Type == CureFlowPermissions.ClaimType)
            .Select(c => c.Value)
            .ToList();
        permissions.Should().BeEquivalentTo(new[]
        {
            CureFlowPermissions.PatientView,
            CureFlowPermissions.AppointmentCreate,
        });
    }

    [Fact]
    public void IssuePlatformUser_marks_platform_ops_without_tenant_id()
    {
        var platformUserId = Guid.NewGuid();
        var jwt = CreateService().IssuePlatformUser(platformUserId, "ops@cureflow.test");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        token.Subject.Should().Be(platformUserId.ToString());
        token.Claims.Should().Contain(c => c.Type == "platform_user" && c.Value == "true");
        token.Claims.Should().Contain(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == "platform_ops");
        token.Claims.Should().NotContain(c => c.Type == "tenant_id");
        token.Claims.Should().NotContain(c => c.Type == CureFlowPermissions.ClaimType);
        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(8), TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void Issue_honors_custom_lifetime()
    {
        var jwt = CreateService().Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "a@b.test",
            "Staff",
            Array.Empty<string>(),
            expires: TimeSpan.FromMinutes(15));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Issue_throws_when_jwt_secret_missing()
    {
        var service = CreateService(secret: null);
        var act = () => service.Issue(Guid.NewGuid(), Guid.NewGuid(), "a@b.test", "Staff", Array.Empty<string>());
        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt:Secret*");
    }
}

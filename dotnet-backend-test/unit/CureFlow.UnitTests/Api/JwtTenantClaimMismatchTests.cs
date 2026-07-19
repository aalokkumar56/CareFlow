using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CureFlow.Api.Middleware;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// JWT tenant claim issuance and TenantMiddleware mismatch handling
/// without spinning up the full ASP.NET host.
/// </summary>
public class JwtTenantClaimMismatchTests
{
    private const string Secret = "unit-test-secret-key-min-32-chars!!";
    private const string Issuer = "cureflow-unit";
    private const string Audience = "cureflow-api-unit";

    [Fact]
    public void Issue_EmbedsTenantIdAndPermissionClaims()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var jwt = CreateJwtService().Issue(
            userId,
            tenantId,
            "admin@hospital.test",
            RoleNames.Admin,
            [CureFlowPermissions.PatientView, CureFlowPermissions.DashboardView]);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        token.Claims.First(c => c.Type == "tenant_id").Value.Should().Be(tenantId.ToString());
        token.Claims.First(c => c.Type is JwtRegisteredClaimNames.Sub or "sub").Value.Should().Be(userId.ToString());
        token.Claims.Where(c => c.Type == CureFlowPermissions.ClaimType)
            .Select(c => c.Value)
            .Should().BeEquivalentTo(
                [CureFlowPermissions.PatientView, CureFlowPermissions.DashboardView]);
        token.Claims.Should().NotContain(c => c.Type == "platform_user");
    }

    [Fact]
    public void IssuePlatformUser_OmitsTenantIdClaim()
    {
        var jwt = CreateJwtService().IssuePlatformUser(Guid.NewGuid(), "ops@cureflow.test");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        token.Claims.Should().Contain(c => c.Type == "platform_user" && c.Value == "true");
        token.Claims.Should().NotContain(c => c.Type == "tenant_id");
    }

    [Fact]
    public void ValidateToken_Rejects_WrongSigningKey()
    {
        var tenantId = Guid.NewGuid();
        var jwt = CreateJwtService().Issue(
            Guid.NewGuid(), tenantId, "a@b.test", RoleNames.Staff, []);

        var act = () => new JwtSecurityTokenHandler().ValidateToken(
            jwt,
            CreateValidationParameters(signingSecret: "different-secret-key-min-32-chars!!!"),
            out _);

        act.Should().Throw<SecurityTokenSignatureKeyNotFoundException>();
    }

    [Fact]
    public void ValidateToken_Accepts_MatchingIssuerAudienceAndKey()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var jwt = CreateJwtService().Issue(
            userId, tenantId, "a@b.test", RoleNames.Admin, [CureFlowPermissions.UserView]);

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            jwt,
            CreateValidationParameters(),
            out var validated);

        principal.Should().NotBeNull();
        validated.Should().BeOfType<JwtSecurityToken>();
        // JwtSecurityTokenHandler may map "sub" → NameIdentifier depending on MapInboundClaims
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        subject.Should().Be(userId.ToString());
        principal.FindFirstValue("tenant_id").Should().Be(tenantId.ToString());
    }

    [Fact]
    public async Task TenantMiddleware_Returns403_WhenJwtTenantDiffersFromUserTenant()
    {
        var jwtTenant = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbTenant = Guid.NewGuid();

        var http = new DefaultHttpContext();
        http.User = AuthenticatedUser(
            new Claim("tenant_id", jwtTenant.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "admin@a.test"),
            new Claim(ClaimTypes.Role, RoleNames.Admin));

        var tenant = new CurrentTenant();
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(d => d.QueryFirstOrDefaultAsync<Guid>(
                It.Is<string>(sql => sql.Contains("Users", StringComparison.Ordinal)),
                It.IsAny<object>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbTenant);

        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(http, tenant, db.Object);

        http.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
        tenant.TenantId.Should().Be(jwtTenant,
            "middleware sets claim tenant before DB check; mismatch must short-circuit before next");
    }

    [Fact]
    public async Task TenantMiddleware_UsesDbTenant_WhenJwtTenantMatches()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var http = new DefaultHttpContext();
        http.User = AuthenticatedUser(
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "admin@a.test"),
            new Claim(ClaimTypes.Role, RoleNames.Admin));

        var tenant = new CurrentTenant();
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(d => d.QueryFirstOrDefaultAsync<Guid>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantId);

        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(http, tenant, db.Object);

        nextCalled.Should().BeTrue();
        http.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        tenant.IsAuthenticated.Should().BeTrue();
        tenant.TenantId.Should().Be(tenantId);
        tenant.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task TenantMiddleware_Returns403_WhenUserMissingInDatabase()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var http = new DefaultHttpContext();
        http.User = AuthenticatedUser(
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var tenant = new CurrentTenant();
        var db = new Mock<ICureFlowDbSession>();
        db.Setup(d => d.QueryFirstOrDefaultAsync<Guid>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.Empty);

        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(http, tenant, db.Object);

        http.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task TenantMiddleware_PlatformUser_BypassesTenantBinding()
    {
        var platformUserId = Guid.NewGuid();
        var http = new DefaultHttpContext();
        http.User = AuthenticatedUser(
            new Claim("platform_user", "true"),
            new Claim(ClaimTypes.NameIdentifier, platformUserId.ToString()),
            new Claim(ClaimTypes.Email, "ops@cureflow.test"));

        var tenant = new CurrentTenant();
        var db = new Mock<ICureFlowDbSession>(MockBehavior.Strict);

        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(http, tenant, db.Object);

        nextCalled.Should().BeTrue();
        tenant.IsAuthenticated.Should().BeTrue();
        tenant.UserId.Should().Be(platformUserId);
        tenant.TenantId.Should().Be(Guid.Empty);
        db.Verify(
            d => d.QueryFirstOrDefaultAsync<Guid>(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void IssuedToken_TenantClaim_DoesNotMatch_ForeignTenantId()
    {
        var homeTenant = Guid.NewGuid();
        var foreignTenant = Guid.NewGuid();
        var jwt = CreateJwtService().Issue(
            Guid.NewGuid(), homeTenant, "admin@home.test", RoleNames.Admin, CureFlowPermissions.All);

        var tokenTenant = new JwtSecurityTokenHandler().ReadJwtToken(jwt)
            .Claims.First(c => c.Type == "tenant_id").Value;

        tokenTenant.Should().Be(homeTenant.ToString());
        tokenTenant.Should().NotBe(foreignTenant.ToString());
    }

    private static JwtTokenService CreateJwtService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = Secret,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
            })
            .Build();
        return new JwtTokenService(config);
    }

    private static TokenValidationParameters CreateValidationParameters(string? signingSecret = null) =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret ?? Secret)),
            ClockSkew = TimeSpan.FromMinutes(2),
        };

    private static ClaimsPrincipal AuthenticatedUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Bearer"));
}

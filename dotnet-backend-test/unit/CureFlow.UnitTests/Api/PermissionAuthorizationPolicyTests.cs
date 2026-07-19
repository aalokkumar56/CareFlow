using System.Security.Claims;
using CureFlow.Api.Authorization;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// Unit tests for RBAC permission claim checks and policy naming
/// (no full host / JWT pipeline required).
/// </summary>
public class PermissionAuthorizationPolicyTests
{
    [Fact]
    public void PolicyName_PrefixesPermissionCode()
    {
        CureFlowPermissions.PolicyName(CureFlowPermissions.PatientView)
            .Should().Be("Permission:Patient.View");
        CureFlowPermissions.PolicyName(CureFlowPermissions.UserEdit)
            .Should().Be("Permission:User.Edit");
    }

    [Fact]
    public void ClaimType_IsPermission() =>
        CureFlowPermissions.ClaimType.Should().Be("permission");

    [Fact]
    public void All_ContainsCanonicalCodes_WithoutDuplicates()
    {
        CureFlowPermissions.All.Should().OnlyHaveUniqueItems();
        CureFlowPermissions.All.Should().Contain(new[]
        {
            CureFlowPermissions.PatientView,
            CureFlowPermissions.AppointmentCreate,
            CureFlowPermissions.BillingView,
            CureFlowPermissions.UserEdit,
            CureFlowPermissions.ClinicalView,
            CureFlowPermissions.DashboardView,
        });
    }

    [Fact]
    public void ViewOnly_IsSubsetOfAll()
    {
        CureFlowPermissions.ViewOnly.Should().OnlyHaveUniqueItems();
        CureFlowPermissions.ViewOnly.Should().BeSubsetOf(CureFlowPermissions.All);
        CureFlowPermissions.ViewOnly.Should().NotContain(CureFlowPermissions.PatientDelete);
        CureFlowPermissions.ViewOnly.Should().NotContain(CureFlowPermissions.UserCreate);
    }

    [Theory]
    [InlineData(UserRole.TenantOwner, RoleNames.SuperAdmin)]
    [InlineData(UserRole.Admin, RoleNames.Admin)]
    [InlineData(UserRole.Doctor, RoleNames.Doctor)]
    [InlineData(UserRole.Reception, RoleNames.Receptionist)]
    [InlineData(UserRole.Marketing, RoleNames.Marketing)]
    [InlineData(UserRole.Nurse, RoleNames.Nurse)]
    [InlineData(UserRole.Staff, RoleNames.Staff)]
    public void MapLegacyRole_MapsToSeededRoleNames(UserRole role, string expected) =>
        CureFlowPermissions.MapLegacyRole(role).Should().Be(expected);

    [Fact]
    public async Task Handler_Succeeds_WhenPermissionClaimPresent()
    {
        var user = PrincipalWithPermissions(CureFlowPermissions.PatientView);
        var requirement = new PermissionRequirement(CureFlowPermissions.PatientView);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_DoesNotSucceed_WhenPermissionClaimMissing()
    {
        var user = PrincipalWithPermissions(CureFlowPermissions.PatientView);
        var requirement = new PermissionRequirement(CureFlowPermissions.PatientDelete);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_DoesNotSucceed_WhenUnauthenticated()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity()); // no auth type
        var requirement = new PermissionRequirement(CureFlowPermissions.DashboardView);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
        user.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_RequiresExactPermissionCode_NotRoleClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, RoleNames.Admin),
            new Claim(ClaimTypes.Role, "Patient.View"),
        ], authenticationType: "Test"));
        var requirement = new PermissionRequirement(CureFlowPermissions.PatientView);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "role claims must not satisfy Permission:* policies; only permission claim type does");
    }

    [Fact]
    public async Task Handler_Succeeds_WhenUserHasMultiplePermissionsIncludingRequired()
    {
        var user = PrincipalWithPermissions(
            CureFlowPermissions.PatientView,
            CureFlowPermissions.AppointmentView,
            CureFlowPermissions.DashboardView);
        var requirement = new PermissionRequirement(CureFlowPermissions.AppointmentView);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public void PlatformUserPolicy_RequiresPlatformUserClaim()
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim("platform_user", "true")
            .Build();

        policy.Requirements.Should().NotBeEmpty();
        policy.Requirements.Should().ContainSingle(r => r is ClaimsAuthorizationRequirement);

        var withClaim = PrincipalWithClaims(
            new Claim("platform_user", "true"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        var withoutClaim = PrincipalWithClaims(
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        HasClaimRequirement(withClaim, "platform_user", "true").Should().BeTrue();
        HasClaimRequirement(withoutClaim, "platform_user", "true").Should().BeFalse();
    }

    private static ClaimsPrincipal PrincipalWithPermissions(params string[] permissions) =>
        PrincipalWithClaims(permissions
            .Select(p => new Claim(CureFlowPermissions.ClaimType, p))
            .ToArray());

    private static ClaimsPrincipal PrincipalWithClaims(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static bool HasClaimRequirement(ClaimsPrincipal user, string claimType, string value) =>
        user.Identity?.IsAuthenticated == true && user.HasClaim(claimType, value);
}

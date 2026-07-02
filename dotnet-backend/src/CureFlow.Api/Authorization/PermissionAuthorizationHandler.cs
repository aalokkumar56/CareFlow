using CureFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace CureFlow.Api.Authorization;

public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasClaim(CureFlowPermissions.ClaimType, requirement.PermissionCode))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

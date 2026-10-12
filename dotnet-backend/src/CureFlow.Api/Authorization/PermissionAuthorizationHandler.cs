using CureFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace CureFlow.Api.Authorization;

public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

public class PermissionAuthorizationHandler(ITenantContext tenant) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (tenant is CurrentTenant current && current.IsAuthenticated && !current.IsPlatformUser
            && current.CurrentPermissions.Contains(requirement.PermissionCode, StringComparer.Ordinal))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}


using System.Security.Claims;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace CureFlow.Api.Filters;

public sealed class ClinicalAuthorizationAuditHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (!result.Succeeded && context.User.Identity?.IsAuthenticated == true && !context.User.HasClaim("platform_user", "true")
            && action is not null && ClinicalReadAuditFilter.IsSensitiveController(action.ControllerName)
            && Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId) && tenantId != Guid.Empty
            && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"), out var actorId) && actorId != Guid.Empty)
        {
            // Only signed actor/tenant claims: never a requested/unknown patient ID.
            var tenant = (CurrentTenant)context.RequestServices.GetRequiredService<ITenantContext>();
            tenant.TenantId = tenantId; tenant.UserId = actorId; tenant.IsAuthenticated = true;
            await context.RequestServices.GetRequiredService<IClinicalAccessAuditService>().RecordAsync(
                new(null, action.ControllerName, null, HttpMethods.IsGet(context.Request.Method) ? "read" : "write", "denied"), context.RequestAborted);
        }
        await _default.HandleAsync(next, context, policy, result);
    }
}

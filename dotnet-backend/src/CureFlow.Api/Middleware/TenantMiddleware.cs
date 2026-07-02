using System.Security.Claims;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;

namespace CureFlow.Api.Middleware;

/// <summary>Reads tenant_id claim from JWT and populates the request-scoped ITenantContext.</summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenantContext, ICureFlowDbSession db)
    {
        if (tenantContext is CurrentTenant current && ctx.User?.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = ctx.User.FindFirstValue("tenant_id");
            var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub");
            if (Guid.TryParse(tenantClaim, out var tid)) current.TenantId = tid;
            if (Guid.TryParse(sub, out var uid)) current.UserId = uid;
            current.UserEmail = ctx.User.FindFirstValue(ClaimTypes.Email);
            current.UserRole = ctx.User.FindFirstValue(ClaimTypes.Role);
            current.IsAuthenticated = true;

            if (current.UserId.HasValue)
            {
                var userTenantId = await db.QueryFirstOrDefaultAsync<Guid>(
                    """
                    SELECT "TenantId" FROM "Users"
                    WHERE "Id" = @UserId AND "IsDeleted" = false
                    LIMIT 1
                    """,
                    new { UserId = current.UserId.Value },
                    ignoreTenant: true);

                if (userTenantId == Guid.Empty)
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                if (current.TenantId != Guid.Empty && current.TenantId != userTenantId)
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                current.TenantId = userTenantId;
            }
            else if (current.TenantId == Guid.Empty)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
        await _next(ctx);
    }
}

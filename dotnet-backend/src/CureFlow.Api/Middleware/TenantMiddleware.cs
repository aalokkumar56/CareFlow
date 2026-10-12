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
            if (ctx.User.HasClaim("platform_user", "true"))
            {
                // A platform principal never acquires hospital API authority.
                if (!ctx.Request.Path.StartsWithSegments("/api/platform"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                current.IsAuthenticated = true;
                current.IsPlatformUser = true;
                var platformSub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub");
                if (!Guid.TryParse(platformSub, out var platformUserId) || platformUserId == Guid.Empty)
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                if (!Guid.TryParse(ctx.User.FindFirstValue("platform_session_id"), out var sessionId) || sessionId == Guid.Empty
                    || !int.TryParse(ctx.User.FindFirstValue("platform_session_revision"), out var revision) || revision < 1)
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                current.PlatformSessionId = sessionId;
                current.PlatformSessionRevision = revision;
                current.UserId = platformUserId;
                current.UserEmail = ctx.User.FindFirstValue(ClaimTypes.Email);
                if (!ctx.Request.Path.Equals(new PathString("/api/platform/auth/logout")))
                    await ctx.RequestServices.GetRequiredService<CureFlow.Infrastructure.Areas.Platform.PlatformAccess>().RequireAsync();
                await _next(ctx);
                return;
            }

            if (ctx.Request.Path.Equals(new PathString("/api/auth/login"))
                || ctx.Request.Path.Equals(new PathString("/api/auth/refresh"))
                || ctx.Request.Path.Equals(new PathString("/api/auth/logout"))
                || ctx.Request.Path.Equals(new PathString("/api/auth/register-tenant")))
            {
                await _next(ctx); // Narrow pre-authentication credential operations establish their own identity authority.
                return;
            }
            if (ctx.Request.Path.StartsWithSegments("/api/platform"))
                throw new DomainException("Hospital sessions cannot access platform operations.", 403);
            var tenantClaim = ctx.User.FindFirstValue("tenant_id");
            var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub");
            if (Guid.TryParse(tenantClaim, out var tid)) current.TenantId = tid;
            if (Guid.TryParse(sub, out var uid)) current.UserId = uid;
            current.UserEmail = ctx.User.FindFirstValue(ClaimTypes.Email);
            current.UserRole = ctx.User.FindFirstValue(ClaimTypes.Role);
            current.IsAuthenticated = true;

            if (ctx.Items["resolved_tenant_id"] is Guid resolvedTenant && resolvedTenant != current.TenantId)
                throw new DomainException("Hospital context does not match this session.", 403);
            if (current.TenantId == Guid.Empty || !current.UserId.HasValue)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (!Guid.TryParse(ctx.User.FindFirstValue("tenant_session_id"), out var family) || family == Guid.Empty
                || !int.TryParse(ctx.User.FindFirstValue("tenant_session_revision"), out var tenantRevision) || tenantRevision < 1)
                throw new DomainException("Session expired or revoked. Please sign in.", 401);
            var user = await ctx.RequestServices.GetRequiredService<CureFlow.Infrastructure.Services.RefreshSessionService>()
                .RequireAccessAsync(family, tenantRevision, current.UserId.Value, current.TenantId, ctx.RequestAborted);
            var permissions = await ctx.RequestServices.GetRequiredService<CureFlow.Infrastructure.Identity.IUserPermissionService>()
                .GetPermissionCodesAsync(user, ctx.RequestAborted);
            current.CurrentPermissions = permissions;
            current.UserRole = user.Role.ToString();
            // Attribute role gates and permission gates both see current authority, never stale JWT capabilities.
            if (ctx.User.Identity is ClaimsIdentity identity)
            {
                foreach (var claim in identity.Claims.Where(x => x.Type == CureFlowPermissions.ClaimType || x.Type == ClaimTypes.Role).ToList())
                    identity.RemoveClaim(claim);
                foreach (var permission in permissions) identity.AddClaim(new(CureFlowPermissions.ClaimType, permission));
                identity.AddClaim(new(ClaimTypes.Role, user.Role.ToString()));
                identity.AddClaim(new(ClaimTypes.Role, user.Role.ToString().ToLowerInvariant()));
                if (user.Role == CureFlow.Domain.Enums.UserRole.TenantOwner) identity.AddClaim(new(ClaimTypes.Role, "tenant_owner"));
            }
        }
        await _next(ctx);
    }
}





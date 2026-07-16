using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;

namespace CureFlow.Api.Middleware;

/// <summary>Blocks hospital CRM APIs until tenant is Active and onboarding is complete.</summary>
public class TenantLifecycleMiddleware
{
    private static readonly HashSet<string> AuthPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/me",
        "/api/auth/session",
    };

    private static readonly HashSet<string> OnboardingPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/onboarding",
        "/api/hospital-profile",
        "/api/integrations",
        "/api/settings",
        "/api/users",
        "/api/auth/register",
    };

    private readonly RequestDelegate _next;

    public TenantLifecycleMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ICureFlowDbSession db, ITenantContext tenantContext)
    {
        if (ctx.User?.Identity?.IsAuthenticated != true
            || ctx.User.HasClaim("platform_user", "true")
            || !tenantContext.IsAuthenticated
            || tenantContext.TenantId == Guid.Empty)
        {
            await _next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? string.Empty;
        if (IsWebhookOrPublic(path))
        {
            await _next(ctx);
            return;
        }

        var tenant = await db.QueryFirstOrDefaultAsync<Tenant>(
            """
            SELECT "LifecycleStatus", "OnboardingComplete", "RejectionReason"
            FROM "Tenants"
            WHERE "Id" = @Id AND "IsDeleted" = false
            LIMIT 1
            """,
            new { Id = tenantContext.TenantId },
            ignoreTenant: true);

        if (tenant == null)
        {
            await WriteBlocked(ctx, "tenant_not_found", "pending_approval");
            return;
        }

        if (tenant.LifecycleStatus == TenantLifecycleStatus.PendingApproval)
        {
            if (AuthPaths.Contains(path))
            {
                await _next(ctx);
                return;
            }

            await WriteBlocked(ctx, "tenant_pending_approval", "pending_approval");
            return;
        }

        if (tenant.LifecycleStatus is TenantLifecycleStatus.Rejected or TenantLifecycleStatus.Suspended)
        {
            if (AuthPaths.Contains(path))
            {
                await _next(ctx);
                return;
            }

            var status = tenant.LifecycleStatus == TenantLifecycleStatus.Rejected ? "rejected" : "suspended";
            await WriteBlocked(ctx, $"tenant_{status}", status, tenant.RejectionReason);
            return;
        }

        if (tenant.LifecycleStatus == TenantLifecycleStatus.Active && !tenant.OnboardingComplete)
        {
            if (AuthPaths.Contains(path)
                || OnboardingPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(ctx);
                return;
            }

            await WriteBlocked(ctx, "onboarding_incomplete", "onboarding_required");
            return;
        }

        await _next(ctx);
    }

    private static bool IsWebhookOrPublic(string path) =>
        path.StartsWith("/api/webhooks", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);

    private static async Task WriteBlocked(HttpContext ctx, string error, string lifecycleStatus, string? reason = null)
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error,
            lifecycle_status = lifecycleStatus,
            rejection_reason = reason,
        }));
    }
}

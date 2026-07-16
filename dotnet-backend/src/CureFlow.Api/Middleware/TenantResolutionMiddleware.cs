using System.Security.Claims;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;

namespace CureFlow.Api.Middleware;

/// <summary>Resolves tenant slug from Host header or X-Tenant-Slug for branded URLs.</summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public TenantResolutionMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx, ICureFlowDbSession db)
    {
        var slug = ResolveSlug(ctx);
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var tenant = await db.QueryFirstOrDefaultAsync<Tenant>(
                """
                SELECT "Id", "Slug", "Name", "LifecycleStatus"
                FROM "Tenants"
                WHERE "Slug" = @Slug AND "IsDeleted" = false
                LIMIT 1
                """,
                new { Slug = slug },
                ignoreTenant: true);

            if (tenant != null)
            {
                ctx.Items["resolved_tenant_slug"] = tenant.Slug;
                ctx.Items["resolved_tenant_name"] = tenant.Name;
                ctx.Items["resolved_tenant_id"] = tenant.Id;
            }
        }

        await _next(ctx);
    }

    private string? ResolveSlug(HttpContext ctx)
    {
        var headerSlug = ctx.Request.Headers["X-Tenant-Slug"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerSlug))
            return headerSlug.Trim().ToLowerInvariant();

        var host = ctx.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host))
            return null;

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return null;

        var subdomain = parts[0].ToLowerInvariant();
        if (subdomain is "www" or "app" or "api" or "localhost")
            return null;

        if (_env.IsDevelopment() && subdomain.EndsWith("-local", StringComparison.Ordinal))
            subdomain = subdomain[..^6];

        return subdomain;
    }
}

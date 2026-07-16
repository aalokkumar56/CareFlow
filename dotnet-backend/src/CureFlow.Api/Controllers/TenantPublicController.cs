using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/public")]
public class TenantPublicController : ControllerBase
{
    private readonly ICureFlowDbSession _db;

    public TenantPublicController(ICureFlowDbSession db) => _db = db;

    /// <summary>Resolve hospital branding by slug (subdomain login).</summary>
    [HttpGet("tenant/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var tenant = await _db.QueryFirstOrDefaultAsync<Tenant>(
            """
            SELECT "Id", "Slug", "Name", "LifecycleStatus"
            FROM "Tenants"
            WHERE "Slug" = @Slug AND "IsDeleted" = false
            LIMIT 1
            """,
            new { Slug = slug.ToLowerInvariant() },
            ignoreTenant: true,
            ct);

        if (tenant == null)
            return NotFound();

        return Ok(new
        {
            slug = tenant.Slug,
            name = tenant.Name,
            lifecycle_status = tenant.LifecycleStatus.ToString(),
        });
    }

    /// <summary>Tenant resolved from Host / X-Tenant-Slug by middleware.</summary>
    [HttpGet("tenant-context")]
    [AllowAnonymous]
    public IActionResult GetContext()
    {
        if (!HttpContext.Items.TryGetValue("resolved_tenant_slug", out var slugObj)
            || slugObj is not string slug)
        {
            return Ok(new { resolved = false });
        }

        HttpContext.Items.TryGetValue("resolved_tenant_name", out var nameObj);
        return Ok(new
        {
            resolved = true,
            slug,
            name = nameObj as string,
        });
    }
}

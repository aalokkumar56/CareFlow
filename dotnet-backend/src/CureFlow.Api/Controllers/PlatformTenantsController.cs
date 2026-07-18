using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/platform/tenants")]
[Authorize(Policy = "PlatformUser")]
public class PlatformTenantsController : ControllerBase
{
    private readonly ICureFlowDbSession _db;

    public PlatformTenantsController(ICureFlowDbSession db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        var sql = """
            SELECT * FROM "Tenants"
            WHERE "IsDeleted" = false
            """;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var lifecycle = ParseLifecycleFilter(status);
            if (lifecycle.HasValue)
                sql += $""" AND "LifecycleStatus" = {(int)lifecycle.Value}""";
        }

        sql += """ ORDER BY "CreatedAt" ASC""";

        var tenants = await _db.QueryAsync<Tenant>(sql, ignoreTenant: true, ct: ct);
        return Ok(tenants.Select(MapTenant));
    }

    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var platformUserId = GetPlatformUserId();
        var updated = await _db.ExecuteAsync(
            """
            UPDATE "Tenants"
            SET "LifecycleStatus" = @status,
                "ApprovedAt" = NOW(),
                "ApprovedByPlatformUserId" = @platformUserId,
                "RejectionReason" = NULL,
                "IsActive" = true,
                "UpdatedAt" = NOW()
            WHERE "Id" = @id AND "IsDeleted" = false
              AND "LifecycleStatus" = @pending
            """,
            new
            {
                id,
                platformUserId,
                status = (int)TenantLifecycleStatus.Active,
                pending = (int)TenantLifecycleStatus.PendingApproval,
            },
            ignoreTenant: true,
            ct: ct);

        if (updated == 0)
            return NotFound(new { error = "Tenant not found or not pending approval" });

        await LogPlatformActionAsync(platformUserId, id, "tenant.approve", null, ct);
        return Ok(new { id, lifecycle_status = TenantLifecycleStatus.Active.ToString() });
    }

    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectTenantRequest? body, CancellationToken ct)
    {
        var platformUserId = GetPlatformUserId();
        var reason = body?.Reason?.Trim();
        var updated = await _db.ExecuteAsync(
            """
            UPDATE "Tenants"
            SET "LifecycleStatus" = @status,
                "RejectionReason" = @reason,
                "IsActive" = false,
                "UpdatedAt" = NOW()
            WHERE "Id" = @id AND "IsDeleted" = false
              AND "LifecycleStatus" = @pending
            """,
            new
            {
                id,
                reason,
                status = (int)TenantLifecycleStatus.Rejected,
                pending = (int)TenantLifecycleStatus.PendingApproval,
            },
            ignoreTenant: true,
            ct: ct);

        if (updated == 0)
            return NotFound(new { error = "Tenant not found or not pending approval" });

        await LogPlatformActionAsync(platformUserId, id, "tenant.reject", new { reason }, ct);
        return Ok(new { id, lifecycle_status = TenantLifecycleStatus.Rejected.ToString(), rejection_reason = reason });
    }

    [HttpPatch("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var platformUserId = GetPlatformUserId();
        var updated = await _db.ExecuteAsync(
            """
            UPDATE "Tenants"
            SET "LifecycleStatus" = @status,
                "IsActive" = false,
                "UpdatedAt" = NOW()
            WHERE "Id" = @id AND "IsDeleted" = false
            """,
            new { id, status = (int)TenantLifecycleStatus.Suspended },
            ignoreTenant: true,
            ct: ct);

        if (updated == 0)
            return NotFound();

        await LogPlatformActionAsync(platformUserId, id, "tenant.suspend", null, ct);
        return Ok(new { id, lifecycle_status = TenantLifecycleStatus.Suspended.ToString() });
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var platformUserId = GetPlatformUserId();
        var updated = await _db.ExecuteAsync(
            """
            UPDATE "Tenants"
            SET "LifecycleStatus" = @status,
                "IsActive" = true,
                "UpdatedAt" = NOW()
            WHERE "Id" = @id AND "IsDeleted" = false
            """,
            new { id, status = (int)TenantLifecycleStatus.Active },
            ignoreTenant: true,
            ct: ct);

        if (updated == 0)
            return NotFound();

        await LogPlatformActionAsync(platformUserId, id, "tenant.activate", null, ct);
        return Ok(new { id, lifecycle_status = TenantLifecycleStatus.Active.ToString() });
    }

    private Guid? GetPlatformUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private async Task LogPlatformActionAsync(Guid? platformUserId, Guid tenantId, string action, object? metadata, CancellationToken ct)
    {
        await _db.InsertAsync(new PlatformAuditLog
        {
            PlatformUserId = platformUserId,
            TenantId = tenantId,
            Action = action,
            MetadataJson = metadata == null ? null : JsonSerializer.Serialize(metadata),
        }, ignoreTenant: true, ct);
    }

    private static TenantLifecycleStatus? ParseLifecycleFilter(string status)
    {
        var key = status.Trim().ToLowerInvariant().Replace("_", "", StringComparison.Ordinal);
        return key switch
        {
            "pendingapproval" => TenantLifecycleStatus.PendingApproval,
            "active" => TenantLifecycleStatus.Active,
            "suspended" => TenantLifecycleStatus.Suspended,
            "rejected" => TenantLifecycleStatus.Rejected,
            _ => Enum.TryParse<TenantLifecycleStatus>(status, true, out var parsed) ? parsed : null,
        };
    }

    private static object MapTenant(Tenant t) => new
    {
        id = t.Id,
        slug = t.Slug,
        name = t.Name,
        contact_email = t.ContactEmail,
        is_active = t.IsActive,
        lifecycle_status = t.LifecycleStatus.ToString(),
        rejection_reason = t.RejectionReason,
        approved_at = t.ApprovedAt,
        onboarding_complete = t.OnboardingComplete,
        timezone = t.Timezone ?? TenantTimeHelper.DefaultTimeZoneId,
        plan = t.Plan.ToString(),
        subscription_status = t.SubscriptionStatus.ToString(),
        trial_ends_at = t.TrialEndsAt,
        created_at = t.CreatedAt,
    };
}

using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.External;

public sealed class ExternalProcessingPolicy(ICureFlowDbSession db, ITenantContext tenant,
    IConfiguration configuration, IClinicalAccessAuditService audit) : IExternalProcessingPolicy
{
    private async Task<TenantProcessingPolicy?> CurrentAsync(string provider, string purpose, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ExternalProcessing:Enabled") || !tenant.IsAuthenticated
            || tenant.IsPlatformUser || tenant.TenantId == Guid.Empty
            || (!tenant.UserId.HasValue && string.IsNullOrWhiteSpace(tenant.ServiceIdentity))) return null;
        var kind = tenant.UserId.HasValue ? "person" : "service";
        var row = await db.QueryFirstOrDefaultAsync<TenantProcessingPolicy>("""
            SELECT * FROM "TenantProcessingPolicies" WHERE "TenantId"=@TenantId
              AND "Provider"=@provider AND "Purpose"=@purpose AND NOT "IsDeleted"
            ORDER BY "Revision" DESC LIMIT 1
            """, new { provider, purpose }, ct: ct);
        if (row is null || !row.IsActive || !row.Allowed || row.Revision < 1 || row.BasisRevision < 1 || row.ActorKind != kind
            || row.ApprovedBy == Guid.Empty || row.ApprovedAt > DateTime.UtcNow || row.ExpiresAt <= DateTime.UtcNow
            || string.IsNullOrWhiteSpace(row.ApprovalReference) || string.IsNullOrWhiteSpace(row.BasisReference)) return null;
        return row;
    }
    public async Task<bool> IsAllowedAsync(string provider, string purpose, CancellationToken ct) =>
        await CurrentAsync(provider, purpose, ct) is not null;
    public async Task RequireDispatchAsync(string provider, string purpose, string resourceType, Guid? resourceId,
        string? resourceVersion, CancellationToken ct)
    {
        var policy = await CurrentAsync(provider, purpose, ct);
        if (policy is null)
        {
            if (tenant.IsAuthenticated && !tenant.IsPlatformUser && tenant.TenantId != Guid.Empty
                && (tenant.UserId.HasValue || !string.IsNullOrWhiteSpace(tenant.ServiceIdentity)))
                await audit.RecordAsync(new(null, "ExternalProcessing", null, "external_dispatch", "denied", Purpose: "operations"), ct);
            throw new DomainException("External processing is not approved for this hospital and purpose.", 403);
        }
        // Technical provenance only: no prompt, clinical body, provider key or generated text.
        await audit.RecordAsync(new(null, resourceType, resourceId, "external_dispatch", "authorized",
            $"policy:{policy.Id:N}:{policy.Revision};basis:{policy.BasisRevision};source:{resourceVersion}", "operations"), ct);
    }
}



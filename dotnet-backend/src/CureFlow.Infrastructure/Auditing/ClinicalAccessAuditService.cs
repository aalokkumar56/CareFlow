using System.Diagnostics;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Ehr;

namespace CureFlow.Infrastructure.Auditing;

public sealed class ClinicalAccessAuditService(ICureFlowDbSession db, ITenantContext tenant) : IClinicalAccessAuditService
{
    public Task RecordAsync(ClinicalAccessEvidence evidence, CancellationToken ct, ICureFlowDbSession? transaction = null) =>
        (transaction ?? db).InsertAsync(Build(tenant, evidence), ct: ct);

    internal static ClinicalAuditEvent Build(ITenantContext tenant, ClinicalAccessEvidence evidence)
    {
        if (!tenant.IsAuthenticated || tenant.IsPlatformUser || tenant.TenantId == Guid.Empty
            || (!tenant.UserId.HasValue && string.IsNullOrWhiteSpace(tenant.ServiceIdentity)))
            throw new DomainException("A verified clinical actor is required.", 403);
        if (evidence.ResourceType.Length > 64 || evidence.Action.Length > 64 || evidence.Purpose.Length > 64)
            throw new InvalidOperationException("Clinical audit vocabulary must remain bounded.");
        return new ClinicalAuditEvent
        {
            TenantId = tenant.TenantId, ActorKind = tenant.UserId.HasValue ? "person" : "service",
            ActorId = tenant.UserId, ServiceIdentity = tenant.UserId.HasValue ? null : tenant.ServiceIdentity,
            PatientId = evidence.Outcome == "denied" ? null : evidence.PatientId,
            ResourceType = evidence.ResourceType, ResourceId = evidence.Outcome == "denied" ? null : evidence.ResourceId,
            ResourceVersion = evidence.ResourceVersion, Action = evidence.Action, Purpose = evidence.Purpose,
            Outcome = evidence.Outcome, CorrelationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")
        };
    }
}

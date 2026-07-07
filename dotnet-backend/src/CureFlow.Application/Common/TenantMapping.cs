using CureFlow.Application.DTOs;
using CureFlow.Domain.Entities.Saas;

namespace CureFlow.Application.Common;

public static class TenantMapping
{
    public static TenantDto ToDto(Tenant tenant) => new(
        tenant.Id,
        tenant.Slug,
        tenant.Name,
        tenant.Plan,
        tenant.SubscriptionStatus,
        tenant.LifecycleStatus,
        tenant.RejectionReason,
        tenant.ApprovedAt,
        tenant.OnboardingComplete,
        tenant.CreatedAt);
}

namespace CureFlow.Domain.Enums;

/// <summary>Hospital tenant approval and lifecycle state (distinct from subscription billing status).</summary>
public enum TenantLifecycleStatus
{
    PendingApproval = 0,
    Active = 1,
    Suspended = 2,
    Rejected = 3,
}

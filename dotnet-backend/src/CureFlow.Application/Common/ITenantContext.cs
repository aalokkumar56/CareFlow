namespace CureFlow.Application.Common;

/// <summary>Resolves the current request's TenantId. Implemented in Api layer (from JWT claim).</summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    Guid? UserId { get; }
    string? UserEmail { get; }
    string? UserRole { get; }
    bool IsAuthenticated { get; }
}



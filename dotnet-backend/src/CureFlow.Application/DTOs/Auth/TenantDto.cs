using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record TenantDto(
    Guid Id,
    string Slug,
    string Name,
    TenantType TenantType,
    SubscriptionPlan Plan,
    SubscriptionStatus Status,
    TenantLifecycleStatus LifecycleStatus,
    string? RejectionReason,
    DateTime? ApprovedAt,
    bool OnboardingComplete,
    string? Timezone,
    DateTime CreatedAt);

public record SessionDto(UserDto User, TenantDto Tenant);

public record OnboardingStateDto(
    bool ProfileComplete,
    bool WhatsAppConnected,
    bool TeamInvited,
    bool IsComplete, IReadOnlyList<string>? RequiredSteps = null);

public record PlatformAuthResponse(string AccessToken, string Email, string Name);

public record PlatformSetupStatusResponse(bool NeedsSetup);

public record PlatformBootstrapRequest(string Name, string Email, string Password);

public record RejectTenantRequest(string? Reason);
public record PlatformSessionResponse(Guid Id, string Email, string Name, string Role, IReadOnlyList<string> Permissions, DateTime ExpiresAt);



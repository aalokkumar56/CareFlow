using System.ComponentModel.DataAnnotations;
using CureFlow.Domain.Enums;
namespace CureFlow.Application.Areas.Platform;

public record PlatformChange([Required, StringLength(500, MinimumLength = 3)] string Reason);
public record FacilityInput(
 [Required, StringLength(120, MinimumLength = 2)] string Name,
 [StringLength(40)] string? Code, [StringLength(240)] string? Address,
 [StringLength(80)] string? City, [StringLength(2, MinimumLength = 2)] string? Country,
 bool IsPrimary, bool IsActive, [Required, StringLength(500, MinimumLength = 3)] string Reason);
public record ModuleInput(
 [Required, RegularExpression("^[a-z][a-z0-9-]{1,39}$")] string Key,
 [Required, StringLength(120, MinimumLength = 2)] string Name,
 [StringLength(500)] string? Description, bool IsCore, bool IsActive,
 [Required, StringLength(500, MinimumLength = 3)] string Reason);
public record EntitlementInput(bool IsEnabled, [Range(0, 100000000)] int? Limit,
 DateTime? ExpiresAt, [Required, StringLength(500, MinimumLength = 3)] string Reason);
public record SubscriptionInput(
 [EnumDataType(typeof(SubscriptionPlan))] SubscriptionPlan Plan,
 [EnumDataType(typeof(SubscriptionStatus))] SubscriptionStatus Status,
 [Range(1, 1000000)] int SeatLimit, [Range(1, 100000000)] int PatientLimit,
 [Range(0, 100000000)] int MessagesQuotaMonthly, DateTime? TrialEndsAt, DateTime? SubscriptionRenewsAt,
 [Required, StringLength(500, MinimumLength = 3)] string Reason);
public record OperatorInput(
 [Required, StringLength(120, MinimumLength = 2)] string Name,
 [Required, EmailAddress, StringLength(254)] string Email,
 [Required, StringLength(60)] string Role,
 bool IsActive, [StringLength(128, MinimumLength = 12)] string? Password,
 [Required, StringLength(500, MinimumLength = 3)] string Reason);
public record RoleInput(
 [Required, RegularExpression("^[a-z][a-z0-9-]{1,39}$")] string Key,
 [Required, StringLength(120, MinimumLength = 2)] string Name,
 [Required] string[] Permissions,
 [Required, StringLength(500, MinimumLength = 3)] string Reason);
public record SupportInput(Guid TenantId, [Required, StringLength(500, MinimumLength = 3)] string Reason,
 [Range(5, 120)] int DurationMinutes = 30);
public record JobInput(Guid TenantId, [Required, StringLength(500, MinimumLength = 3)] string Reason);
public record TenantTypeInput([EnumDataType(typeof(TenantType))] TenantType TenantType,
 [Required, StringLength(500, MinimumLength = 3)] string Reason);
public record TenantProfileInput(
 [Required, StringLength(120, MinimumLength = 2)] string Name,
 [StringLength(120)] string? ContactName,
 [EmailAddress, StringLength(254)] string? ContactEmail,
 [StringLength(25)] string? ContactPhone,
 [StringLength(2, MinimumLength = 2)] string? Country,
 [Required, StringLength(100)] string Timezone,
 [Required, StringLength(500, MinimumLength = 3)] string Reason);
public static class PlatformPermissions
{
 public static readonly string[] All = ["overview.read","tenants.read","tenants.write","modules.read","modules.write","facilities.read","facilities.write","entitlements.read","entitlements.write","billing.read","billing.write","operators.read","operators.write","roles.read","roles.write","audit.read","support.read","support.write","operations.read","operations.write"];
 public static bool Can(string role, IEnumerable<string> permissions, string permission) => role == "owner" || permissions.Contains(permission);
 public static bool CanTransition(TenantLifecycleStatus state, string action) => (state, action) switch {
  (TenantLifecycleStatus.PendingApproval, "review") => true,
 (TenantLifecycleStatus.PendingApproval, "approve" or "reject") => true,
 (TenantLifecycleStatus.Active, "suspend") => true,
 (TenantLifecycleStatus.Suspended, "activate") => true,
 (TenantLifecycleStatus.Rejected, "reopen") => true, _ => false };
}


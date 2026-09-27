using System.ComponentModel.DataAnnotations;
using CureFlow.Domain.Enums;
namespace CureFlow.Application.Areas.Platform;

public record PlatformChange([property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public record FacilityInput(
 [property: Required, StringLength(120, MinimumLength = 2)] string Name,
 [property: StringLength(40)] string? Code, [property: StringLength(240)] string? Address,
 [property: StringLength(80)] string? City, [property: StringLength(2, MinimumLength = 2)] string? Country,
 bool IsPrimary, bool IsActive, [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public record ModuleInput(
 [property: Required, RegularExpression("^[a-z][a-z0-9-]{1,39}$")] string Key,
 [property: Required, StringLength(120, MinimumLength = 2)] string Name,
 [property: StringLength(500)] string? Description, bool IsCore, bool IsActive,
 [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public record EntitlementInput(bool IsEnabled, [property: Range(0, 100000000)] int? Limit,
 DateTime? ExpiresAt, [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public record SubscriptionInput(
 [property: EnumDataType(typeof(SubscriptionPlan))] SubscriptionPlan Plan,
 [property: EnumDataType(typeof(SubscriptionStatus))] SubscriptionStatus Status,
 [property: Range(1, 1000000)] int SeatLimit, [property: Range(1, 100000000)] int PatientLimit,
 [property: Range(0, 100000000)] int MessagesQuotaMonthly, DateTime? TrialEndsAt, DateTime? SubscriptionRenewsAt,
 [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public record OperatorInput(
 [property: Required, StringLength(120, MinimumLength = 2)] string Name,
 [property: Required, EmailAddress, StringLength(254)] string Email,
 [property: Required, StringLength(60)] string Role,
 bool IsActive, [property: StringLength(128, MinimumLength = 12)] string? Password,
 [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public record RoleInput(
 [property: Required, RegularExpression("^[a-z][a-z0-9-]{1,39}$")] string Key,
 [property: Required, StringLength(120, MinimumLength = 2)] string Name,
 [property: Required] string[] Permissions,
 [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public record SupportInput(Guid TenantId, [property: Required, StringLength(500, MinimumLength = 3)] string Reason,
 [property: Range(5, 120)] int DurationMinutes = 30);
public record JobInput(Guid TenantId, [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public record TenantTypeInput([property: EnumDataType(typeof(TenantType))] TenantType TenantType,
 [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public record TenantProfileInput(
 [property: Required, StringLength(120, MinimumLength = 2)] string Name,
 [property: StringLength(120)] string? ContactName,
 [property: EmailAddress, StringLength(254)] string? ContactEmail,
 [property: StringLength(25)] string? ContactPhone,
 [property: StringLength(2, MinimumLength = 2)] string? Country,
 [property: Required, StringLength(100)] string Timezone,
 [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public static class PlatformPermissions
{
 public static readonly string[] All = ["overview.read","tenants.read","tenants.write","modules.read","modules.write","facilities.read","facilities.write","entitlements.read","entitlements.write","billing.read","billing.write","operators.read","operators.write","roles.read","roles.write","audit.read","support.read","support.write","operations.read","operations.write"];
 public static bool Can(string role, IEnumerable<string> permissions, string permission) => role == "owner" || permissions.Contains(permission);
 public static bool CanTransition(TenantLifecycleStatus state, string action) => (state, action) switch {
 (TenantLifecycleStatus.PendingApproval, "approve" or "reject") => true,
 (TenantLifecycleStatus.Active, "suspend") => true,
 (TenantLifecycleStatus.Suspended, "activate") => true,
 (TenantLifecycleStatus.Rejected, "reopen") => true, _ => false };
}

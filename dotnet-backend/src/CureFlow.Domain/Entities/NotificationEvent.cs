using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

/// <summary>
/// A single notification event (fan-out-on-read). Exactly ONE row is written per event,
/// regardless of how many users will eventually see it. Per-user state lives in
/// <see cref="NotificationReceipt"/>; the feed is materialized at read time.
/// </summary>
public class NotificationEvent : TenantEntity
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? ActionUrl { get; set; }
    public string? DedupeKey { get; set; }
    public string MetadataJson { get; set; } = "{}";

    /// <summary>User who triggered the event. They are NEVER shown their own event in the feed.</summary>
    public Guid? CreatedByUserId { get; set; }

    public NotificationAudienceMode AudienceMode { get; set; } = NotificationAudienceMode.Permission;

    /// <summary>
    /// For <see cref="NotificationAudienceMode.Directed"/>: a JSON array of user-id strings that may see the event.
    /// Stored as text and parsed in code (never bound as a List&lt;string&gt; Dapper parameter).
    /// </summary>
    public string? TargetUserIdsJson { get; set; }

    /// <summary>Denormalized comma-separated required permissions copied from the type definition.</summary>
    public string? RequiredPermissionsCsv { get; set; }
}

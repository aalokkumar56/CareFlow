using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

/// <summary>
/// Per-user feed cursor enabling O(1) "mark all read" and ensuring brand-new users do not
/// inherit pre-existing broadcast events.
/// </summary>
public class NotificationFeedCursor : TenantEntity
{
    public Guid UserId { get; set; }

    /// <summary>Events created at or before this instant are treated as read (set by mark-all-read).</summary>
    public DateTime? LastReadAllAt { get; set; }

    /// <summary>Only events created at/after this instant are visible. Defaults to the user's CreatedAt.</summary>
    public DateTime FeedSince { get; set; }
}

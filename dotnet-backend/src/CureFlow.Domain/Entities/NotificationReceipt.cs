using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities;

/// <summary>
/// Lazy per-user state for a <see cref="NotificationEvent"/>. A row is only created when a user
/// reads or dismisses an event — broadcasts do not pre-create receipts.
/// </summary>
public class NotificationReceipt : TenantEntity
{
    public Guid NotificationEventId { get; set; }
    public Guid UserId { get; set; }
    public DateTime? ReadAt { get; set; }

    /// <summary>Per-user hide (delete). The event row remains visible to other users.</summary>
    public DateTime? DismissedAt { get; set; }
}

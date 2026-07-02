namespace CureFlow.Domain.Enums;

/// <summary>
/// How a <c>NotificationEvent</c> determines who can see it in the fan-out-on-read feed.
/// </summary>
public enum NotificationAudienceMode
{
    /// <summary>Event is visible to an explicit, small set of users (e.g. the assignee).</summary>
    Directed = 0,

    /// <summary>Event is visible to every user holding the required permissions (broadcast, no per-user rows).</summary>
    Permission = 1,
}

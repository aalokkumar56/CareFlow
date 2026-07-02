using CureFlow.Domain.Enums;

namespace CureFlow.Application.Notifications;

/// <summary>
/// Resolved audience for a notification event. For <see cref="NotificationAudienceMode.Directed"/>
/// the <see cref="TargetUserIds"/> hold the explicit recipients (creator already excluded);
/// for <see cref="NotificationAudienceMode.Permission"/> the list is empty and visibility is
/// computed at read time from <see cref="RequiredPermissions"/>.
/// </summary>
public sealed record NotificationAudience(
    NotificationAudienceMode Mode,
    IReadOnlyList<Guid> TargetUserIds,
    IReadOnlyList<string> RequiredPermissions);

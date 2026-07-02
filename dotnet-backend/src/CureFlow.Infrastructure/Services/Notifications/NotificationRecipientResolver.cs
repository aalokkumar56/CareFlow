using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Enums;

namespace CureFlow.Infrastructure.Services.Notifications;

/// <summary>
/// Fan-out-on-read resolver. It never enumerates permission holders: directed events carry a
/// tiny explicit recipient list, permission events defer audience evaluation to read time.
/// </summary>
public class NotificationRecipientResolver : INotificationRecipientResolver
{
    public NotificationAudience ResolveAudience(
        NotificationTypeDefinition definition,
        NotificationPublishRequest request)
    {
        var directed = definition.RecipientStrategy switch
        {
            NotificationRecipientStrategy.Assignee => ResolveDirectedTargets(request),
            NotificationRecipientStrategy.AssignedStaff => ResolveDirectedTargets(request),
            _ => Array.Empty<Guid>(),
        };

        // Directed strategies with a concrete target set go to exactly those users.
        // Everything else (PermissionHolders, Admins, AppointmentDoctor, or an unassigned
        // AssignedStaff event) becomes a permission broadcast resolved lazily at read time.
        if (directed.Count > 0)
        {
            var targets = ExcludeCreator(directed, request.CreatorUserId);
            return new NotificationAudience(
                NotificationAudienceMode.Directed,
                targets,
                definition.RequiredPermissions);
        }

        return new NotificationAudience(
            NotificationAudienceMode.Permission,
            Array.Empty<Guid>(),
            definition.RequiredPermissions);
    }

    private static IReadOnlyList<Guid> ResolveDirectedTargets(NotificationPublishRequest request)
    {
        if (request.TargetUserIds is { Count: > 0 })
            return request.TargetUserIds.Distinct().ToList();
        if (request.AssignedStaffId.HasValue)
            return [request.AssignedStaffId.Value];
        return Array.Empty<Guid>();
    }

    private static IReadOnlyList<Guid> ExcludeCreator(IReadOnlyList<Guid> targets, Guid? creatorUserId)
    {
        if (!creatorUserId.HasValue)
            return targets;
        return targets.Where(id => id != creatorUserId.Value).ToList();
    }
}

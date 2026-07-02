using CureFlow.Application.DTOs.Notification;
using CureFlow.Application.Notifications;

namespace CureFlow.Application.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(bool unreadOnly, int limit, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface INotificationPublisher
{
    Task PublishAsync(NotificationPublishRequest request, CancellationToken ct = default);
}

public interface INotificationRecipientResolver
{
    /// <summary>
    /// Maps the type's recipient strategy onto an <see cref="NotificationAudience"/> for the
    /// fan-out-on-read model. Directed events resolve a small explicit target set (no DB
    /// enumeration); permission events fan out lazily at read time.
    /// </summary>
    NotificationAudience ResolveAudience(
        NotificationTypeDefinition definition,
        NotificationPublishRequest request);
}

public interface INotificationPreferenceService
{
    Task<IReadOnlyList<NotificationPreferenceDto>> GetEffectivePreferencesAsync(CancellationToken ct = default);
    Task UpdateUserPreferencesAsync(UpdateNotificationPreferencesRequest request, CancellationToken ct = default);
    Task ResetToRoleDefaultsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoleNotificationDefaultDto>> GetRoleDefaultsAsync(CancellationToken ct = default);
    Task UpdateRoleDefaultsAsync(IReadOnlyList<RoleNotificationDefaultDto> updates, CancellationToken ct = default);
    Task<bool> IsInAppEnabledForUserAsync(Guid userId, string notificationType, CancellationToken ct = default);
}

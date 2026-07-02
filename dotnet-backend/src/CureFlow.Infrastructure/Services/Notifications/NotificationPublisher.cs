using System.Text.Json;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services.Notifications;

/// <summary>
/// Fan-out-on-write was removed in favour of fan-out-on-read: a broadcast now writes exactly
/// ONE <see cref="NotificationEvent"/> row regardless of how many users will eventually see it.
/// Preference and RBAC filtering happen at read time (events are shared across users).
/// </summary>
public class NotificationPublisher(
    ICureFlowDbSession db,
    INotificationRecipientResolver resolver,
    ILogger<NotificationPublisher> logger) : INotificationPublisher
{
    public async Task PublishAsync(NotificationPublishRequest request, CancellationToken ct = default)
    {
        if (!NotificationTypeDefinitions.TryGet(request.Type, out var definition) || definition == null)
        {
            logger.LogWarning("Skipping notification publish for unknown type {Type}", request.Type);
            return;
        }

        var audience = resolver.ResolveAudience(definition, request);

        // A directed event with no resolvable recipients (e.g. assignee was the creator) has no audience.
        if (audience.Mode == NotificationAudienceMode.Directed && audience.TargetUserIds.Count == 0)
        {
            logger.LogDebug("Skipping directed notification {Type} with no recipients", request.Type);
            return;
        }

        try
        {
            // Event-level dedupe: at most one event per (tenant, type, dedupe key).
            if (!string.IsNullOrWhiteSpace(request.DedupeKey))
            {
                var exists = await db.QueryFirstOrDefaultAsync<Guid?>(
                    """
                    SELECT "Id" FROM "NotificationEvents"
                    WHERE "TenantId" = @TenantId AND "Type" = @type
                      AND "DedupeKey" = @dedupeKey AND "IsDeleted" = false
                    LIMIT 1
                    """,
                    new { type = request.Type, dedupeKey = request.DedupeKey },
                    ct: ct);
                if (exists.HasValue) return;
            }

            var evt = new NotificationEvent
            {
                Type = request.Type,
                Title = request.Title,
                Body = request.Body,
                Severity = request.Severity,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                ActionUrl = request.ActionUrl,
                DedupeKey = request.DedupeKey,
                CreatedByUserId = request.CreatorUserId ?? db.UserId,
                AudienceMode = audience.Mode,
                TargetUserIdsJson = audience.Mode == NotificationAudienceMode.Directed
                    ? JsonSerializer.Serialize(audience.TargetUserIds.Select(id => id.ToString()))
                    : null,
                RequiredPermissionsCsv = audience.RequiredPermissions.Count > 0
                    ? string.Join(',', audience.RequiredPermissions)
                    : null,
            };

            await db.InsertAsync(evt, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish notification event {Type}", request.Type);
        }

        // Phase 3 TODO: SignalR push to group tenant:{tid}:user:{uid} for directed/permission audiences.
        // Phase 3 TODO: enqueue email via IntegrationEvents outbox for the email channel.
    }
}

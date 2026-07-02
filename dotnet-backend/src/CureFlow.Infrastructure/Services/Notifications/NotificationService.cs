using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs.Notification;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.Notifications;

/// <summary>
/// Fan-out-on-read feed. The feed for a user is materialized at read time from shared
/// <c>NotificationEvents</c>, joined to lazy per-user <c>NotificationReceipts</c> and a
/// per-user <c>NotificationFeedCursor</c>. A user never sees an event they triggered, nor a
/// type they lack permission for or have disabled in preferences.
/// </summary>
public class NotificationService(
    ICureFlowDbSession db,
    ITenantContext tenant,
    IUserPermissionService permissions,
    INotificationPreferenceService preferences) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(bool unreadOnly, int limit, CancellationToken ct = default)
    {
        if (!tenant.UserId.HasValue)
            return Array.Empty<NotificationDto>();

        var allowedTypes = await GetFeedAllowedTypesAsync(ct);
        if (allowedTypes.Length == 0) return Array.Empty<NotificationDto>();

        var userId = tenant.UserId.Value;
        var cursor = await EnsureCursorAsync(userId, ct);
        var take = Math.Clamp(limit, 1, 100);
        var unreadFilter = unreadOnly
            ? """ AND r."ReadAt" IS NULL AND (@lastReadAllAt::timestamptz IS NULL OR e."CreatedAt" > @lastReadAllAt::timestamptz)"""
            : "";

        var rows = await db.QueryAsync<FeedRow>(
            $"""
            SELECT e."Id" AS "Id", e."Type" AS "Type", e."Title" AS "Title", e."Body" AS "Body",
                   e."Severity" AS "Severity", e."EntityType" AS "EntityType", e."EntityId" AS "EntityId",
                   e."ActionUrl" AS "ActionUrl", e."CreatedAt" AS "CreatedAt", r."ReadAt" AS "ReceiptReadAt"
            FROM "NotificationEvents" e
            LEFT JOIN "NotificationReceipts" r
                ON r."NotificationEventId" = e."Id" AND r."UserId" = @userId
               AND r."TenantId" = @TenantId AND r."IsDeleted" = false
            WHERE e."TenantId" = @TenantId AND e."IsDeleted" = false
              AND e."Type" = ANY(@allowedTypes)
              AND (e."CreatedByUserId" IS NULL OR e."CreatedByUserId" <> @userId)
              AND e."CreatedAt" >= @feedSince
              AND r."DismissedAt" IS NULL
              AND {AudiencePredicate}{unreadFilter}
            ORDER BY e."CreatedAt" DESC
            LIMIT @take
            """,
            new
            {
                userId,
                userIdText = userId.ToString(),
                allowedTypes,
                feedSince = cursor.FeedSince,
                lastReadAllAt = cursor.LastReadAllAt,
                take,
            },
            ct: ct);

        return rows.Select(r => Map(r, cursor.LastReadAllAt)).ToList();
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        if (!tenant.UserId.HasValue) return 0;

        var allowedTypes = await GetFeedAllowedTypesAsync(ct);
        if (allowedTypes.Length == 0) return 0;

        var userId = tenant.UserId.Value;
        var cursor = await EnsureCursorAsync(userId, ct);

        return await db.QuerySingleAsync<int>(
            $"""
            SELECT COUNT(*)::int
            FROM "NotificationEvents" e
            LEFT JOIN "NotificationReceipts" r
                ON r."NotificationEventId" = e."Id" AND r."UserId" = @userId
               AND r."TenantId" = @TenantId AND r."IsDeleted" = false
            WHERE e."TenantId" = @TenantId AND e."IsDeleted" = false
              AND e."Type" = ANY(@allowedTypes)
              AND (e."CreatedByUserId" IS NULL OR e."CreatedByUserId" <> @userId)
              AND e."CreatedAt" >= @feedSince
              AND r."DismissedAt" IS NULL
              AND {AudiencePredicate}
              AND r."ReadAt" IS NULL
              AND (@lastReadAllAt::timestamptz IS NULL OR e."CreatedAt" > @lastReadAllAt::timestamptz)
            """,
            new
            {
                userId,
                userIdText = userId.ToString(),
                allowedTypes,
                feedSince = cursor.FeedSince,
                lastReadAllAt = cursor.LastReadAllAt,
            },
            ct: ct);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        if (!tenant.UserId.HasValue)
            throw new ForbiddenException("Not authenticated");

        var userId = tenant.UserId.Value;
        var evt = await GetVisibleEventAsync(id, userId, ct);
        await UpsertReceiptAsync(evt.Id, userId, markRead: true, markDismissed: false, ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        if (!tenant.UserId.HasValue) return;

        var cursor = await EnsureCursorAsync(tenant.UserId.Value, ct);
        cursor.LastReadAllAt = DateTime.UtcNow;
        await db.UpdateAsync(cursor, ct: ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!tenant.UserId.HasValue)
            throw new ForbiddenException("Not authenticated");

        var userId = tenant.UserId.Value;
        var evt = await GetVisibleEventAsync(id, userId, ct);
        await UpsertReceiptAsync(evt.Id, userId, markRead: false, markDismissed: true, ct);
    }

    /// <summary>Directed membership OR a permission broadcast (RBAC ceiling enforced separately).</summary>
    private const string AudiencePredicate =
        """
        (
            e."AudienceMode" = 1
            OR (e."AudienceMode" = 0 AND EXISTS (
                SELECT 1 FROM jsonb_array_elements_text(
                    COALESCE(NULLIF(e."TargetUserIdsJson", ''), '[]')::jsonb) AS t(uid)
                WHERE LOWER(t.uid) = @userIdText))
        )
        """;

    private async Task<NotificationEvent> GetVisibleEventAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var evt = await db.QueryFirstOrDefaultAsync<NotificationEvent>(
            """
            SELECT * FROM "NotificationEvents"
            WHERE "Id" = @id AND "TenantId" = @TenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { id },
            ct: ct) ?? throw new NotFoundException("Notification");

        // Creator never sees (or acts on) their own event.
        if (evt.CreatedByUserId.HasValue && evt.CreatedByUserId.Value == userId)
            throw new NotFoundException("Notification");

        // RBAC ceiling: the requesting user must hold the type's required permissions.
        var rbacAllowed = await GetRbacAllowedTypesAsync(ct);
        if (!rbacAllowed.Contains(evt.Type, StringComparer.Ordinal))
            throw new ForbiddenException("Notification type not permitted");

        if (evt.AudienceMode == NotificationAudienceMode.Directed && !IsDirectedTarget(evt, userId))
            throw new NotFoundException("Notification");

        return evt;
    }

    private static bool IsDirectedTarget(NotificationEvent evt, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(evt.TargetUserIdsJson))
            return false;
        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(evt.TargetUserIdsJson) ?? [];
            return ids.Any(s => Guid.TryParse(s, out var g) && g == userId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task UpsertReceiptAsync(Guid eventId, Guid userId, bool markRead, bool markDismissed, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var existing = await db.QueryFirstOrDefaultAsync<NotificationReceipt>(
            """
            SELECT * FROM "NotificationReceipts"
            WHERE "NotificationEventId" = @eventId AND "UserId" = @userId
              AND "TenantId" = @TenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { eventId, userId },
            ct: ct);

        if (existing == null)
        {
            await db.InsertAsync(new NotificationReceipt
            {
                NotificationEventId = eventId,
                UserId = userId,
                ReadAt = markRead ? now : null,
                DismissedAt = markDismissed ? now : null,
            }, ct: ct);
            return;
        }

        if (markRead && existing.ReadAt == null)
            existing.ReadAt = now;
        if (markDismissed)
            existing.DismissedAt = now;
        await db.UpdateAsync(existing, ct: ct);
    }

    private async Task<NotificationFeedCursor> EnsureCursorAsync(Guid userId, CancellationToken ct)
    {
        var cursor = await LoadCursorAsync(userId, ct);
        if (cursor != null) return cursor;

        var user = await db.GetByIdAsync<User>(userId, ct: ct);
        cursor = new NotificationFeedCursor
        {
            UserId = userId,
            FeedSince = user?.CreatedAt ?? DateTime.UtcNow,
            LastReadAllAt = null,
        };

        try
        {
            await db.InsertAsync(cursor, ct: ct);
        }
        catch
        {
            // Concurrent first-read created the cursor; re-read the winning row.
            cursor = await LoadCursorAsync(userId, ct) ?? cursor;
        }

        return cursor;
    }

    private async Task<NotificationFeedCursor?> LoadCursorAsync(Guid userId, CancellationToken ct) =>
        await db.QueryFirstOrDefaultAsync<NotificationFeedCursor>(
            """
            SELECT * FROM "NotificationFeedCursors"
            WHERE "UserId" = @userId AND "TenantId" = @TenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { userId },
            ct: ct);

    /// <summary>Types the user may see (RBAC) AND has enabled in preferences — applied at read time.</summary>
    private async Task<string[]> GetFeedAllowedTypesAsync(CancellationToken ct)
    {
        var prefs = await preferences.GetEffectivePreferencesAsync(ct);
        return prefs.Where(p => p.InAppEnabled).Select(p => p.NotificationType).ToArray();
    }

    /// <summary>Types the user may see based on permissions only (the RBAC ceiling for mark/delete).</summary>
    private async Task<string[]> GetRbacAllowedTypesAsync(CancellationToken ct)
    {
        if (!tenant.UserId.HasValue) return Array.Empty<string>();

        var user = await db.GetByIdAsync<User>(tenant.UserId.Value, ct: ct);
        if (user == null) return Array.Empty<string>();

        var userPerms = await permissions.GetPermissionCodesAsync(user, ct);
        return NotificationTypeDefinitions.All
            .Where(d => NotificationRbac.HasAllPermissions(userPerms, d.RequiredPermissions))
            .Select(d => d.Code)
            .ToArray();
    }

    private static NotificationDto Map(FeedRow r, DateTime? lastReadAllAt)
    {
        var isRead = r.ReceiptReadAt.HasValue
            || (lastReadAllAt.HasValue && r.CreatedAt <= lastReadAllAt.Value);
        return new NotificationDto
        {
            Id = r.Id,
            Type = r.Type,
            Title = r.Title,
            Body = r.Body,
            Severity = r.Severity,
            EntityType = r.EntityType,
            EntityId = r.EntityId,
            ActionUrl = r.ActionUrl,
            IsRead = isRead,
            ReadAt = r.ReceiptReadAt ?? (isRead ? lastReadAllAt : null),
            CreatedAt = r.CreatedAt,
        };
    }

    private sealed class FeedRow
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public NotificationSeverity Severity { get; set; }
        public string? EntityType { get; set; }
        public Guid? EntityId { get; set; }
        public string? ActionUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReceiptReadAt { get; set; }
    }
}

using CureFlow.Application.Common;
using CureFlow.Application.DTOs.Notification;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.Notifications;

public class NotificationPreferenceService(
    ICureFlowDbSession db,
    ITenantContext tenant,
    IUserPermissionService permissions) : INotificationPreferenceService
{
    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetEffectivePreferencesAsync(CancellationToken ct = default)
    {
        if (!tenant.UserId.HasValue)
            return Array.Empty<NotificationPreferenceDto>();

        var user = await db.GetByIdAsync<User>(tenant.UserId.Value, ct: ct)
            ?? throw new NotFoundException("User");
        var userPerms = await permissions.GetPermissionCodesAsync(user, ct);
        var roleName = await GetPrimaryRoleNameAsync(user.Id, ct);

        var userPrefs = await LoadUserPreferencesAsync(user.Id, ct);
        var roleDefaults = await LoadRoleDefaultsForUserAsync(user.Id, ct);

        return NotificationTypeDefinitions.All
            .Select(def =>
            {
                var canConfigure = NotificationRbac.HasAllPermissions(userPerms, def.RequiredPermissions);
                var roleDefault = ResolveRoleDefault(def, roleName, roleDefaults);
                // Missing dictionary key must stay null — bool TryGetValue defaults to false and would
                // incorrectly override role/system defaults.
                bool? userOverride = userPrefs.TryGetValue(def.Code, out var stored) ? stored : null;
                var effective = NotificationPreferenceResolver.ResolveInAppEnabled(
                    canConfigure, userOverride, roleDefault, def.DefaultEnabled);

                return new NotificationPreferenceDto
                {
                    NotificationType = def.Code,
                    Category = def.Category,
                    Label = FormatLabel(def.Code),
                    Description = FormatDescription(def.Code),
                    InAppEnabled = effective,
                    InAppUserOverride = userOverride,
                    InAppRoleDefault = roleDefault ?? def.DefaultEnabled,
                    CanConfigure = canConfigure,
                };
            })
            .Where(p => p.CanConfigure)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Label)
            .ToList();
    }

    public async Task UpdateUserPreferencesAsync(UpdateNotificationPreferencesRequest request, CancellationToken ct = default)
    {
        if (!tenant.UserId.HasValue)
            throw new ForbiddenException("Not authenticated");

        var user = await db.GetByIdAsync<User>(tenant.UserId.Value, ct: ct)
            ?? throw new NotFoundException("User");
        var userPerms = await permissions.GetPermissionCodesAsync(user, ct);

        foreach (var item in request.Preferences)
        {
            if (!NotificationTypeDefinitions.TryGet(item.NotificationType, out var def) || def == null)
                continue;
            if (!NotificationRbac.HasAllPermissions(userPerms, def.RequiredPermissions))
                throw new ForbiddenException($"Cannot configure notification type {item.NotificationType}");

            var existing = await db.QueryFirstOrDefaultAsync<NotificationPreference>(
                """
                SELECT * FROM "NotificationPreferences"
                WHERE "UserId" = @userId AND "NotificationType" = @type
                  AND "Channel" = @channel AND "TenantId" = @TenantId
                LIMIT 1
                """,
                new { userId = user.Id, type = item.NotificationType, channel = (int)NotificationChannel.InApp },
                ct: ct);

            if (existing == null)
            {
                await db.InsertAsync(new NotificationPreference
                {
                    UserId = user.Id,
                    NotificationType = item.NotificationType,
                    Channel = NotificationChannel.InApp,
                    Enabled = item.InAppEnabled,
                    UpdatedAt = DateTime.UtcNow,
                }, ct: ct);
            }
            else
            {
                // UpdateAsync WHERE requires IsDeleted=false, so soft-deleted rows must be
                // undeleted via explicit SQL (reset-to-defaults soft-deletes overrides).
                await db.ExecuteAsync(
                    """
                    UPDATE "NotificationPreferences"
                    SET "Enabled" = @enabled,
                        "IsDeleted" = false,
                        "UpdatedAt" = @now
                    WHERE "Id" = @id AND "TenantId" = @TenantId
                    """,
                    new
                    {
                        id = existing.Id,
                        enabled = item.InAppEnabled,
                        now = DateTime.UtcNow,
                    },
                    ct: ct);
            }
        }
    }

    public async Task ResetToRoleDefaultsAsync(CancellationToken ct = default)
    {
        if (!tenant.UserId.HasValue) return;

        await db.ExecuteAsync(
            """
            UPDATE "NotificationPreferences"
            SET "IsDeleted" = true, "UpdatedAt" = @now
            WHERE "UserId" = @userId AND "TenantId" = @TenantId AND "IsDeleted" = false
            """,
            new { userId = tenant.UserId.Value, now = DateTime.UtcNow },
            ct: ct);
    }

    public async Task<IReadOnlyList<RoleNotificationDefaultDto>> GetRoleDefaultsAsync(CancellationToken ct = default)
    {
        var roles = await db.QueryAsync<Role>(
            """
            SELECT * FROM "Roles"
            WHERE "IsDeleted" = false
            ORDER BY "Name"
            """,
            ignoreTenant: true,
            ct: ct);

        var defaults = await db.QueryAsync<RoleNotificationDefault>(
            """
            SELECT * FROM "RoleNotificationDefaults"
            WHERE "IsDeleted" = false AND "Channel" = @channel
            """,
            new { channel = (int)NotificationChannel.InApp },
            ignoreTenant: true,
            ct: ct);

        var rolePerms = await LoadRolePermissionMapAsync(ct);
        var result = new List<RoleNotificationDefaultDto>();

        foreach (var role in roles)
        {
            rolePerms.TryGetValue(role.Id, out var codes);
            codes ??= Array.Empty<string>();

            foreach (var def in NotificationTypeDefinitions.All)
            {
                var stored = defaults.FirstOrDefault(d => d.RoleId == role.Id && d.NotificationType == def.Code);
                var canEnable = NotificationRbac.HasAllPermissions(codes, def.RequiredPermissions);
                var enabled = stored?.Enabled
                    ?? NotificationTypeDefinitions.IsEnabledForRole(role.Name, def);

                result.Add(new RoleNotificationDefaultDto
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    NotificationType = def.Code,
                    Category = def.Category,
                    InAppEnabled = enabled && canEnable,
                    CanEnable = canEnable,
                });
            }
        }

        return result;
    }

    public async Task UpdateRoleDefaultsAsync(IReadOnlyList<RoleNotificationDefaultDto> updates, CancellationToken ct = default)
    {
        foreach (var item in updates)
        {
            var def = NotificationTypeDefinitions.TryGet(item.NotificationType, out var d) ? d : null;
            if (def == null) continue;

            var rolePerms = await LoadRolePermissionMapAsync(ct);
            rolePerms.TryGetValue(item.RoleId, out var codes);
            codes ??= Array.Empty<string>();
            var canEnable = NotificationRbac.HasAllPermissions(codes, def.RequiredPermissions);
            if (!canEnable) continue;

            var existing = await db.QueryFirstOrDefaultAsync<RoleNotificationDefault>(
                """
                SELECT * FROM "RoleNotificationDefaults"
                WHERE "RoleId" = @roleId AND "NotificationType" = @type
                  AND "Channel" = @channel AND "IsDeleted" = false
                LIMIT 1
                """,
                new { roleId = item.RoleId, type = item.NotificationType, channel = (int)NotificationChannel.InApp },
                ignoreTenant: true,
                ct: ct);

            if (existing == null)
            {
                await db.InsertAsync(new RoleNotificationDefault
                {
                    RoleId = item.RoleId,
                    NotificationType = item.NotificationType,
                    Channel = NotificationChannel.InApp,
                    Enabled = item.InAppEnabled,
                    IsSystem = false,
                }, ignoreTenant: true, ct: ct);
            }
            else
            {
                existing.Enabled = item.InAppEnabled;
                existing.IsSystem = false;
                await db.UpdateAsync(existing, ignoreTenant: true, ct: ct);
            }
        }
    }

    public async Task<bool> IsInAppEnabledForUserAsync(Guid userId, string notificationType, CancellationToken ct = default)
    {
        if (!NotificationTypeDefinitions.TryGet(notificationType, out var def) || def == null)
            return false;

        var user = await db.QueryFirstOrDefaultAsync<User>(
            """
            SELECT * FROM "Users"
            WHERE "Id" = @userId AND "TenantId" = @TenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { userId },
            ct: ct);
        if (user == null) return false;

        var userPerms = await permissions.GetPermissionCodesAsync(user, ct);
        if (!NotificationRbac.HasAllPermissions(userPerms, def.RequiredPermissions))
            return false;

        var userPrefs = await LoadUserPreferencesAsync(userId, ct);
        bool? userOverride = userPrefs.TryGetValue(notificationType, out var stored) ? stored : null;

        var roleName = await GetPrimaryRoleNameAsync(userId, ct);
        var roleDefaults = await LoadRoleDefaultsForUserAsync(userId, ct);
        var roleDefault = ResolveRoleDefault(def, roleName, roleDefaults);

        return NotificationPreferenceResolver.ResolveInAppEnabled(
            true, userOverride, roleDefault, def.DefaultEnabled);
    }

    private async Task<Dictionary<string, bool>> LoadUserPreferencesAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.QueryAsync<NotificationPreference>(
            """
            SELECT * FROM "NotificationPreferences"
            WHERE "UserId" = @userId AND "TenantId" = @TenantId
              AND "Channel" = @channel AND "IsDeleted" = false
            """,
            new { userId, channel = (int)NotificationChannel.InApp },
            ct: ct);

        return rows.ToDictionary(r => r.NotificationType, r => r.Enabled, StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, bool>> LoadRoleDefaultsForUserAsync(Guid userId, CancellationToken ct)
    {
        var roleIds = (await db.QueryAsync<Guid>(
            """
            SELECT "RoleId" FROM "UserRoleAssignments"
            WHERE "UserId" = @userId AND "TenantId" = @TenantId AND "IsDeleted" = false
            """,
            new { userId },
            ct: ct)).ToArray();

        if (roleIds.Length == 0) return new Dictionary<string, bool>(StringComparer.Ordinal);

        var rows = await db.QueryAsync<RoleNotificationDefault>(
            """
            SELECT * FROM "RoleNotificationDefaults"
            WHERE "RoleId" = ANY(@roleIds) AND "Channel" = @channel AND "IsDeleted" = false
            """,
            new { roleIds, channel = (int)NotificationChannel.InApp },
            ignoreTenant: true,
            ct: ct);

        return rows.GroupBy(r => r.NotificationType)
            .ToDictionary(g => g.Key, g => g.Any(x => x.Enabled), StringComparer.Ordinal);
    }

    private async Task<string?> GetPrimaryRoleNameAsync(Guid userId, CancellationToken ct)
    {
        return await db.QueryFirstOrDefaultAsync<string>(
            """
            SELECT r."Name"
            FROM "UserRoleAssignments" ura
            INNER JOIN "Roles" r ON r."Id" = ura."RoleId"
            WHERE ura."UserId" = @userId AND ura."TenantId" = @TenantId AND ura."IsDeleted" = false
            ORDER BY ura."CreatedAt" ASC
            LIMIT 1
            """,
            new { userId },
            ct: ct);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadRolePermissionMapAsync(CancellationToken ct)
    {
        var rows = await db.QueryAsync<(Guid RoleId, string Code)>(
            """
            SELECT rp."RoleId", p."Code"
            FROM "RolePermissions" rp
            INNER JOIN "Permissions" p ON p."Id" = rp."PermissionId"
            WHERE rp."IsDeleted" = false AND p."IsDeleted" = false
            """,
            ignoreTenant: true,
            ct: ct);

        return rows.GroupBy(r => r.RoleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Code).Distinct().ToList());
    }

    private static bool? ResolveRoleDefault(
        NotificationTypeDefinition def,
        string? roleName,
        Dictionary<string, bool> roleDefaults)
    {
        if (roleDefaults.TryGetValue(def.Code, out var fromDb))
            return fromDb;
        if (!string.IsNullOrWhiteSpace(roleName))
            return NotificationTypeDefinitions.IsEnabledForRole(roleName, def);
        return null;
    }

    private static string FormatLabel(string code) =>
        code.Split('.').Last().Replace('_', ' ');

    private static string FormatDescription(string code) => code switch
    {
        NotificationTypeCodes.WhatsappInboundMessage => "New patient or lead message in WhatsApp inbox",
        NotificationTypeCodes.WhatsappLeadEscalation => "Lead awaiting reply for over 15 minutes",
        NotificationTypeCodes.AppointmentCreated => "New appointment scheduled",
        NotificationTypeCodes.AppointmentNoShow => "Patient marked as no-show",
        NotificationTypeCodes.TaskAssigned => "Task assigned to you",
        NotificationTypeCodes.CampaignCompleted => "Marketing campaign finished sending",
        NotificationTypeCodes.ClinicalLabUploaded => "Lab report uploaded for a patient",
        NotificationTypeCodes.PatientCreated => "A new patient was registered in the system",
        _ => "Notification",
    };
}

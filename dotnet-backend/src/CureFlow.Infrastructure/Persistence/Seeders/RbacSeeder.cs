using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Persistence.Seeders;

/// <summary>Seeds global RBAC reference data: permission groups, permissions, roles, and role-permission mappings.</summary>
public static class RbacSeeder
{
    public static async Task SeedAsync(ICureFlowDbSession db, CancellationToken ct = default)
    {
        await SeedPermissionGroupsAsync(db, ct);
        await SeedPermissionsAsync(db, ct);
        await SeedRolesAsync(db, ct);
        await SeedRolePermissionsAsync(db, ct);
        await SeedAppointmentQuickTemplatesAsync(db, ct);
        await MarketingCalendarSeeder.SeedAsync(db, ct);
        await TemplatePlaceholderSeeder.SeedAsync(db, ct);
    }

    private static async Task SeedAppointmentQuickTemplatesAsync(ICureFlowDbSession db, CancellationToken ct)
    {
        var tenants = await db.QueryAsync<Tenant>(
            """
            SELECT * FROM "Tenants"
            WHERE "IsActive" = true AND "IsDeleted" = false
            """,
            ignoreTenant: true,
            ct: ct);

        var definitions = new (string Category, string Name, string Body)[]
        {
            (AppointmentTemplateKeys.Confirmation, "Appointment Confirmation",
                "Namaste {name}, your appointment with {doctor} on {date} at {time} is confirmed. Please reach 15 min early. - Cure & Care Hospital"),
            (AppointmentTemplateKeys.Rescheduled, "Appointment Rescheduled",
                "Namaste {name}, your appointment with {doctor} ({department}) has been rescheduled to {date} at {time}. Please reach 15 min early. - Cure & Care Hospital"),
            (AppointmentTemplateKeys.Cancelled, "Appointment Cancelled",
                "Namaste {name}, your appointment with {doctor} on {date} at {time} has been cancelled. Reply here to rebook. - Cure & Care Hospital"),
        };

        foreach (var tenant in tenants)
        {
            var existingCategories = (await db.QueryAsync<string>(
                """
                SELECT "Category" FROM "Templates"
                WHERE "TenantId" = @tenantId AND "IsDeleted" = false
                """,
                new { tenantId = tenant.Id },
                ignoreTenant: true,
                ct: ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (category, name, body) in definitions)
            {
                if (existingCategories.Contains(category))
                    continue;

                await db.InsertAsync(new QuickTemplate
                {
                    TenantId = tenant.Id,
                    Name = name,
                    Category = category,
                    Body = body,
                }, ignoreTenant: true, ct: ct);
                existingCategories.Add(category);
            }
        }
    }

    private static async Task SeedPermissionGroupsAsync(ICureFlowDbSession db, CancellationToken ct)
    {
        var groups = new (string Name, string? Description)[]
        {
            ("Patient", "Patient CRM"),
            ("Appointment", "Appointments"),
            ("Billing", "Billing"),
            ("User", "User management"),
            ("Staff", "Staff profiles and schedules"),
            ("WhatsApp", "WhatsApp integration"),
            ("Conversation", "Inbox & conversations"),
            ("Campaign", "Marketing campaigns"),
            ("Clinical", "Clinical / EHR records"),
            ("Dashboard", "Dashboards"),
            ("Audit", "Audit logs"),
            ("Settings", "Hospital settings"),
            ("Referral", "Referral CRM"),
        };

        foreach (var (name, description) in groups)
        {
            var exists = await db.QueryFirstOrDefaultAsync<int>(
                """
                SELECT 1 FROM "PermissionGroups"
                WHERE "Name" = @name AND "IsDeleted" = false
                LIMIT 1
                """,
                new { name },
                ignoreTenant: true,
                ct: ct) == 1;
            if (exists) continue;

            await db.InsertAsync(new PermissionGroup { Name = name, Description = description }, ignoreTenant: true, ct: ct);
        }
    }

    private static async Task SeedPermissionsAsync(ICureFlowDbSession db, CancellationToken ct)
    {
        var groupIds = (await db.QueryAsync<PermissionGroup>(
            """SELECT * FROM "PermissionGroups" WHERE "IsDeleted" = false""",
            ignoreTenant: true,
            ct: ct)).ToDictionary(g => g.Name, g => g.Id);

        foreach (var code in CureFlowPermissions.All)
        {
            var exists = await db.QueryFirstOrDefaultAsync<int>(
                """
                SELECT 1 FROM "Permissions"
                WHERE "Code" = @code AND "IsDeleted" = false
                LIMIT 1
                """,
                new { code },
                ignoreTenant: true,
                ct: ct) == 1;
            if (exists) continue;

            var groupName = code.Split('.')[0];
            groupIds.TryGetValue(groupName, out var groupId);

            await db.InsertAsync(new Permission
            {
                Code = code,
                Name = code.Replace('.', ' '),
                PermissionGroupId = groupId == Guid.Empty ? null : groupId,
            }, ignoreTenant: true, ct: ct);
        }
    }

    private static async Task SeedRolesAsync(ICureFlowDbSession db, CancellationToken ct)
    {
        var roles = new (string Name, string Description)[]
        {
            (RoleNames.SuperAdmin, "Full platform access for tenant owner"),
            (RoleNames.Admin, "Hospital administrator"),
            (RoleNames.Receptionist, "Front desk and inbox operator"),
            (RoleNames.Doctor, "Clinical staff"),
            (RoleNames.Nurse, "Nursing staff"),
            (RoleNames.Marketing, "Campaigns and lead analytics"),
            (RoleNames.Staff, "General hospital staff"),
            (RoleNames.Viewer, "Read-only access"),
        };

        foreach (var (name, description) in roles)
        {
            var exists = await db.QueryFirstOrDefaultAsync<int>(
                """
                SELECT 1 FROM "Roles"
                WHERE "Name" = @name AND "IsDeleted" = false
                LIMIT 1
                """,
                new { name },
                ignoreTenant: true,
                ct: ct) == 1;
            if (exists) continue;

            await db.InsertAsync(new Role { Name = name, Description = description, IsSystem = true }, ignoreTenant: true, ct: ct);
        }
    }

    private static async Task SeedRolePermissionsAsync(ICureFlowDbSession db, CancellationToken ct)
    {
        var permissions = (await db.QueryAsync<Permission>(
            """SELECT * FROM "Permissions" WHERE "IsDeleted" = false""",
            ignoreTenant: true,
            ct: ct)).ToDictionary(p => p.Code, p => p.Id);

        var roles = (await db.QueryAsync<Role>(
            """SELECT * FROM "Roles" WHERE "IsDeleted" = false""",
            ignoreTenant: true,
            ct: ct)).ToDictionary(r => r.Name, r => r.Id);

        var mappings = new Dictionary<string, IReadOnlyList<string>>
        {
            [RoleNames.SuperAdmin] = CureFlowPermissions.All,
            [RoleNames.Admin] = CureFlowPermissions.All,
            [RoleNames.Doctor] = Filter(
                CureFlowPermissions.PatientView, CureFlowPermissions.PatientEdit,
                CureFlowPermissions.AppointmentView, CureFlowPermissions.AppointmentCreate, CureFlowPermissions.AppointmentEdit,
                CureFlowPermissions.ClinicalView, CureFlowPermissions.ClinicalEdit,
                CureFlowPermissions.ConversationView,
                CureFlowPermissions.StaffView,
                CureFlowPermissions.DashboardView),
            [RoleNames.Receptionist] = Filter(
                CureFlowPermissions.PatientView, CureFlowPermissions.PatientCreate, CureFlowPermissions.PatientEdit,
                CureFlowPermissions.AppointmentView, CureFlowPermissions.AppointmentCreate, CureFlowPermissions.AppointmentEdit,
                CureFlowPermissions.ConversationView, CureFlowPermissions.ConversationManage,
                CureFlowPermissions.WhatsAppView, CureFlowPermissions.WhatsAppSend,
                CureFlowPermissions.StaffView,
                CureFlowPermissions.DashboardView),
            [RoleNames.Nurse] = Filter(
                CureFlowPermissions.PatientView,
                CureFlowPermissions.AppointmentView,
                CureFlowPermissions.ClinicalView, CureFlowPermissions.ClinicalEdit,
                CureFlowPermissions.StaffView,
                CureFlowPermissions.DashboardView),
            [RoleNames.Marketing] = Filter(
                CureFlowPermissions.PatientView,
                CureFlowPermissions.CampaignView, CureFlowPermissions.CampaignManage,
                CureFlowPermissions.WhatsAppView, CureFlowPermissions.WhatsAppSend,
                CureFlowPermissions.DashboardView),
            [RoleNames.Staff] = Filter(
                CureFlowPermissions.PatientView,
                CureFlowPermissions.ConversationView,
                CureFlowPermissions.ReferralView, CureFlowPermissions.ReferralManage,
                CureFlowPermissions.DashboardView),
            [RoleNames.Viewer] = CureFlowPermissions.ViewOnly,
        };

        var existingPairs = (await db.QueryAsync<(Guid RoleId, Guid PermissionId)>(
            """SELECT "RoleId", "PermissionId" FROM "RolePermissions" WHERE "IsDeleted" = false""",
            ignoreTenant: true,
            ct: ct)).ToHashSet();

        foreach (var (roleName, codes) in mappings)
        {
            if (!roles.TryGetValue(roleName, out var roleId))
                continue;

            foreach (var code in codes)
            {
                if (!permissions.TryGetValue(code, out var permissionId))
                    continue;

                if (existingPairs.Contains((roleId, permissionId)))
                    continue;

                await db.InsertAsync(new RolePermission { RoleId = roleId, PermissionId = permissionId }, ignoreTenant: true, ct: ct);
                existingPairs.Add((roleId, permissionId));
            }
        }
    }

    private static IReadOnlyList<string> Filter(params string[] codes) => codes;
}

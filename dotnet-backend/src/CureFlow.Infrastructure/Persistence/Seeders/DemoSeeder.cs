using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Rbac;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Dapper;
using Npgsql;

namespace CureFlow.Infrastructure.Persistence.Seeders;

/// <summary>Bootstraps a fresh tenant with a single admin account. Idempotent.</summary>
public static class DemoSeeder
{
    public const string DefaultAdminEmail = "admin@cureflow.in";
    public const string DefaultAdminPassword = "admin123";

    public static async Task SeedAsync(ICureFlowDbSession db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        Tenant? tenant;
        try
        {
            tenant = await db.QueryFirstOrDefaultAsync<Tenant>(
                """
                SELECT * FROM "Tenants"
                WHERE "Slug" = @slug AND "IsDeleted" = false
                """,
                new { slug = "cureandcare" },
                ignoreTenant: true,
                ct: ct);

            if (tenant == null)
            {
                var anyTenant = await db.QueryFirstOrDefaultAsync<int>(
                    """SELECT 1 FROM "Tenants" LIMIT 1""",
                    ignoreTenant: true,
                    ct: ct);
                if (anyTenant == 1)
                    return;
            }
        }
        catch (Npgsql.PostgresException)
        {
            return;
        }

        if (tenant == null)
        {
            tenant = new Tenant
            {
                Slug = "cureandcare",
                Name = "Cure & Care Hospital",
                ContactEmail = DefaultAdminEmail,
                Plan = SubscriptionPlan.Trial,
                SubscriptionStatus = SubscriptionStatus.Trialing,
                TrialEndsAt = DateTime.UtcNow.AddDays(30),
            };
            await db.InsertAsync(tenant, ignoreTenant: true, ct: ct);
        }

        tenant.LifecycleStatus = TenantLifecycleStatus.Active;
        tenant.OnboardingComplete = true;
        tenant.IsActive = true;
        tenant.ApprovedAt ??= DateTime.UtcNow;
        await db.UpdateAsync(tenant, ignoreTenant: true, ct: ct);

        var admin = await db.QueryFirstOrDefaultAsync<User>(
            """
            SELECT * FROM "Users"
            WHERE "TenantId" = @tenantId AND LOWER("Email") = @email AND "IsDeleted" = false
            """,
            new { tenantId = tenant.Id, email = DefaultAdminEmail.ToLower() },
            ignoreTenant: true,
            ct: ct);

        if (admin == null)
        {
            admin = new User
            {
                TenantId = tenant.Id,
                Name = "Admin",
                Email = DefaultAdminEmail,
                Role = UserRole.TenantOwner,
                PasswordHash = hasher.Hash(DefaultAdminPassword),
            };
            await db.InsertAsync(admin, ignoreTenant: true, ct: ct);
        }

        await EnsureUserRoleAssignmentAsync(db, admin, ct);

        var hasProfile = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "HospitalProfiles"
            WHERE "TenantId" = @tenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { tenantId = tenant.Id },
            ignoreTenant: true,
            ct: ct) == 1;

        if (!hasProfile)
        {
            await db.InsertAsync(new HospitalProfile
            {
                TenantId = tenant.Id,
                Name = "Cure & Care Hospital",
                Tagline = "Trust, Transparency, True-Care",
                WorkingHours = "OPD: 9am-8pm · Emergency: 24x7",
                Emergency24x7 = true,
                DepartmentsJson = """["General Medicine","Cardiology","Orthopedics","Pediatrics"]""",
            }, ignoreTenant: true, ct: ct);
        }
    }

    private static async Task EnsureUserRoleAssignmentAsync(ICureFlowDbSession db, User user, CancellationToken ct)
    {
        var exists = await db.QueryFirstOrDefaultAsync<int>(
            """
            SELECT 1 FROM "UserRoleAssignments"
            WHERE "UserId" = @userId AND "TenantId" = @tenantId AND "IsDeleted" = false
            LIMIT 1
            """,
            new { userId = user.Id, tenantId = user.TenantId },
            ignoreTenant: true,
            ct: ct) == 1;
        if (exists) return;

        var roleId = await db.QueryFirstOrDefaultAsync<Guid>(
            """
            SELECT "Id" FROM "Roles"
            WHERE "Name" = @roleName AND "IsDeleted" = false
            LIMIT 1
            """,
            new { roleName = CureFlowPermissions.MapLegacyRole(user.Role) },
            ignoreTenant: true,
            ct: ct);
        if (roleId == Guid.Empty) return;

        await db.InsertAsync(new UserRoleAssignment
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            RoleId = roleId,
        }, ignoreTenant: true, ct: ct);
    }
}

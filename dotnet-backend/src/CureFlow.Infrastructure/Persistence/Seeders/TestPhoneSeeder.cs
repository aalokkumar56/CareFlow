using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Persistence.Seeders;

/// <summary>
/// Points CRM phone fields at the shared WhatsApp test handset so inbound
/// webhooks from 7600174070 match patients and referral doctors.
/// When <paramref name="scopeToTenantSlug"/> is set, only that hospital is updated
/// (preserves per-tenant phones for multi-hospital E2E).
/// </summary>
public static class TestPhoneSeeder
{
    public static async Task SeedAsync(
        ICureFlowDbSession db,
        ILogger logger,
        string? scopeToTenantSlug = null,
        CancellationToken ct = default)
    {
        var phone = WhatsappPhoneHelper.TestPhoneNormalized;
        Guid? tenantId = null;

        if (!string.IsNullOrWhiteSpace(scopeToTenantSlug))
        {
            tenantId = await db.QueryFirstOrDefaultAsync<Guid?>(
                """
                SELECT "Id" FROM "Tenants"
                WHERE "Slug" = @slug AND "IsDeleted" = false
                LIMIT 1
                """,
                new { slug = scopeToTenantSlug },
                ignoreTenant: true,
                ct: ct);

            if (tenantId is null || tenantId == Guid.Empty)
            {
                logger.LogWarning(
                    "TestPhoneSeeder: tenant slug {Slug} not found — skipping scoped test phone seed",
                    scopeToTenantSlug);
                return;
            }
        }

        var tenantFilter = tenantId.HasValue ? """ AND "TenantId" = @tenantId""" : string.Empty;

        var patients = await db.ExecuteAsync(
            $"""
            UPDATE "Patients"
            SET "Phone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false AND "Phone" IS DISTINCT FROM @phone{tenantFilter}
            """,
            new { phone, tenantId },
            ignoreTenant: true,
            ct: ct);

        var doctors = await db.ExecuteAsync(
            $"""
            UPDATE "ReferringDoctors"
            SET "Phone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false
              AND ("Phone" IS NULL OR "Phone" IS DISTINCT FROM @phone){tenantFilter}
            """,
            new { phone, tenantId },
            ignoreTenant: true,
            ct: ct);

        var users = await db.ExecuteAsync(
            $"""
            UPDATE "Users"
            SET "Phone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false
              AND ("Phone" IS NULL OR "Phone" IS DISTINCT FROM @phone){tenantFilter}
            """,
            new { phone, tenantId },
            ignoreTenant: true,
            ct: ct);

        var appointments = await db.ExecuteAsync(
            $"""
            UPDATE "Appointments"
            SET "PatientPhone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false AND "PatientPhone" IS DISTINCT FROM @phone{tenantFilter}
            """,
            new { phone, tenantId },
            ignoreTenant: true,
            ct: ct);

        var conversations = await db.ExecuteAsync(
            $"""
            UPDATE "Conversations"
            SET "WaPhone" = @phone, "UpdatedAt" = NOW()
            WHERE "IsDeleted" = false AND "WaPhone" IS DISTINCT FROM @phone{tenantFilter}
            """,
            new { phone, tenantId },
            ignoreTenant: true,
            ct: ct);

        logger.LogInformation(
            "Test phone {Phone} applied (scope={Scope}) — patients:{Patients} doctors:{Doctors} users:{Users} appointments:{Appointments} conversations:{Conversations}",
            phone,
            scopeToTenantSlug ?? "all",
            patients,
            doctors,
            users,
            appointments,
            conversations);
    }
}

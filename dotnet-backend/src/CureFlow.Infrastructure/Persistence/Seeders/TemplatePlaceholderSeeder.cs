using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Persistence.Seeders;

public static class TemplatePlaceholderSeeder
{
    private static readonly (string Key, string Label, string Description, string Example)[] SystemPlaceholders =
    [
        ("name", "Patient name", "Full name of the patient", "Rajesh Kumar"),
        ("date", "Appointment date", "Scheduled date", "29 May 2026"),
        ("time", "Appointment time", "Scheduled time (hospital local)", "10:30 AM"),
        ("time_with_zone", "Appointment time with zone", "Hospital-local time with timezone label", "10:30 AM IST"),
        ("timezone_abbr", "Timezone abbreviation", "Hospital timezone short label", "IST"),
        ("doctor", "Doctor name", "Assigned doctor or specialist", "Dr. Sharma"),
        ("department", "Department", "Department or specialty", "Cardiology"),
        ("hospital", "Hospital name", "Your hospital display name", "Cure & Care Hospital"),
        ("phone", "Patient phone", "Patient mobile number", "+91 98765 43210"),
    ];

    public static async Task SeedAsync(ICureFlowDbSession db, CancellationToken ct = default)
    {
        var tenants = await db.QueryAsync<Tenant>(
            """
            SELECT * FROM "Tenants"
            WHERE "IsActive" = true AND "IsDeleted" = false
            """,
            ignoreTenant: true,
            ct: ct);

        foreach (var tenant in tenants)
            await SeedForTenantAsync(db, tenant, ct);
    }

    public static async Task SeedForTenantAsync(ICureFlowDbSession db, Tenant tenant, CancellationToken ct)
    {
        var existingKeys = (await db.QueryAsync<string>(
            """
            SELECT "Key" FROM "TemplatePlaceholders"
            WHERE "TenantId" = @tenantId AND "IsSystem" = true AND "IsDeleted" = false
            """,
            new { tenantId = tenant.Id },
            ignoreTenant: true,
            ct: ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, label, description, example) in SystemPlaceholders)
        {
            if (existingKeys.Contains(key))
                continue;

            await db.InsertAsync(new TemplatePlaceholder
            {
                TenantId = tenant.Id,
                Key = key,
                Label = label,
                Description = description,
                Example = example,
                IsSystem = true,
            }, ignoreTenant: true, ct: ct);
        }
    }
}

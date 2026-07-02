using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeds Indian marketing calendar events per tenant.
/// Festival dates (Diwali, Holi, Eid) use approximate Gregorian dates for the seed year — verify annually.
/// </summary>
public static class MarketingCalendarSeeder
{
    private static readonly (string Name, DateOnly Date, string Category, string Message)[] DefaultEvents =
    [
        ("Republic Day", new DateOnly(2026, 1, 26), "national_holiday",
            "🇮🇳 Happy Republic Day from Cure & Care Hospital! Wishing you health and happiness."),
        ("Holi", new DateOnly(2026, 3, 14), "festival",
            "🎨 Happy Holi! May your life be filled with vibrant health. — Cure & Care Hospital"),
        ("Eid al-Fitr (approx.)", new DateOnly(2026, 3, 21), "festival",
            "Eid Mubarak from Cure & Care Hospital! Wishing you peace and good health."),
        ("World Health Day", new DateOnly(2026, 4, 7), "health_day",
            "Your health matters every day. Book a preventive check-up at Cure & Care Hospital."),
        ("International Nurses Day", new DateOnly(2026, 5, 12), "health_day",
            "Thank you to our nursing heroes! Wishing you wellness from Cure & Care Hospital."),
        ("Monsoon Health Tips", new DateOnly(2026, 6, 15), "seasonal",
            "Monsoon is here! Stay hydrated and protect against seasonal illnesses. — Cure & Care Hospital"),
        ("International Yoga Day", new DateOnly(2026, 6, 21), "health_day",
            "🧘 Happy International Yoga Day! Prioritize mind-body wellness with Cure & Care Hospital."),
        ("Doctor's Day", new DateOnly(2026, 7, 1), "health_day",
            "Thank you to all doctors for their dedication. Wishing you good health from Cure & Care Hospital."),
        ("Independence Day", new DateOnly(2026, 8, 15), "national_holiday",
            "🇮🇳 Happy Independence Day! Wishing you health and happiness. — Cure & Care Hospital"),
        ("Diwali (approx.)", new DateOnly(2026, 11, 8), "festival",
            "🪔 Happy Diwali! Wishing you a season of wellness. — Cure & Care Hospital"),
        ("Christmas", new DateOnly(2026, 12, 25), "holiday",
            "Season's greetings from Cure & Care Hospital! Stay warm and stay healthy."),
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
        var existing = await db.QueryAsync<(string Name, DateOnly EventDate)>(
            """
            SELECT "Name", "EventDate" FROM "MarketingCalendarEvents"
            WHERE "TenantId" = @tenantId AND "IsDeleted" = false
            """,
            new { tenantId = tenant.Id },
            ignoreTenant: true,
            ct: ct);

        var existingSet = existing
            .Select(e => $"{e.Name}|{e.EventDate}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var e in DefaultEvents)
        {
            if (existingSet.Contains($"{e.Name}|{e.Date}"))
                continue;

            await db.InsertAsync(Ev(tenant.Id, e.Name, e.Date, e.Category, e.Message), ignoreTenant: true, ct: ct);
        }
    }

    private static MarketingCalendarEvent Ev(Guid tenantId, string name, DateOnly date, string category, string message) =>
        new()
        {
            TenantId = tenantId,
            Name = name,
            EventDate = date,
            Category = category,
            Region = "IN",
            Source = "seed",
            SuggestedMessage = message,
        };
}

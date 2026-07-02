using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;
using System.Text.Json;

namespace CureFlow.Infrastructure.Services;

internal static class DepartmentCatalog
{
    public static async Task<IReadOnlyList<string>> LoadAsync(ICureFlowDbSession db, CancellationToken ct = default)
    {
        var hospitalWhere = SqlFragments.WhereActive<HospitalProfile>(ignoreTenant: false);
        var hospital = await db.QueryFirstOrDefaultAsync<HospitalDepartmentsRow>(
            $"""SELECT "DepartmentsJson" FROM "HospitalProfiles" WHERE {hospitalWhere} LIMIT 1""", ct: ct);
        var fromHospital = ParseNames(hospital?.DepartmentsJson);

        var staffWhere = SqlFragments.WhereActive<StaffProfile>(ignoreTenant: false);
        var fromStaff = await db.QueryAsync<string>(
            $"""
            SELECT DISTINCT "Department" FROM "StaffProfiles"
            WHERE {staffWhere}
              AND "Department" IS NOT NULL AND "Department" <> ''
            ORDER BY "Department"
            """, ct: ct);

        var merged = fromHospital
            .Union(fromStaff, StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return merged.Count > 0 ? merged : DefaultNames;
    }

    private static readonly string[] DefaultNames =
    [
        "General Medicine",
        "Cardiology",
        "Orthopedics",
        "Pediatrics",
    ];

    internal static List<string> ParseNames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new();

            return doc.RootElement.EnumerateArray()
                .Select(ParseElement)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new();
        }
    }

    private static string? ParseElement(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Object when el.TryGetProperty("name", out var name) => name.GetString(),
            _ => null,
        };

    private sealed class HospitalDepartmentsRow
    {
        public string? DepartmentsJson { get; set; }
    }
}

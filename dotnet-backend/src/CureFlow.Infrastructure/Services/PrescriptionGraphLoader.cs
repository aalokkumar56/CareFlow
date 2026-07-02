using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Ehr;

namespace CureFlow.Infrastructure.Services;

internal static class PrescriptionGraphLoader
{
    public static async Task<IReadOnlyList<Prescription>> LoadAsync(
        ICureFlowDbSession db,
        string whereClause,
        object? param,
        string? orderByLimit,
        CancellationToken ct = default)
    {
        var sql = $"""
            SELECT * FROM "Prescriptions"
            WHERE {whereClause}
            {orderByLimit ?? ""}
            """;
        var rxList = (await db.QueryAsync<Prescription>(sql, param, ct: ct)).ToList();
        if (rxList.Count == 0)
            return rxList;

        var ids = rxList.Select(r => r.Id).ToArray();
        var items = (await db.QueryAsync<PrescriptionItem>(
            """SELECT * FROM "PrescriptionItems" WHERE "PrescriptionId" = ANY(@ids)""",
            new { ids }, ct: ct)).ToList();
        var injections = (await db.QueryAsync<Injection>(
            """SELECT * FROM "Injections" WHERE "PrescriptionId" = ANY(@ids)""",
            new { ids }, ct: ct)).ToList();

        var itemsByRx = items.GroupBy(i => i.PrescriptionId).ToDictionary(g => g.Key, g => g.ToList());
        var injByRx = injections.GroupBy(i => i.PrescriptionId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var rx in rxList)
        {
            rx.Items = itemsByRx.GetValueOrDefault(rx.Id) ?? new List<PrescriptionItem>();
            rx.Injections = injByRx.GetValueOrDefault(rx.Id) ?? new List<Injection>();
        }

        return rxList;
    }
}

using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;
using System.Text.Json;

namespace CureFlow.Infrastructure.Services.Query;

public class TagService : ITagService
{
    private readonly ICureFlowDbSession _db;

    public TagService(ICureFlowDbSession db) => _db = db;

    public async Task<IReadOnlyList<object>> ListAsync(CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
        var tagJsonRows = await _db.QueryAsync<string>(
            $"""
            SELECT "Tags"::text FROM "Patients"
            WHERE {where} AND "Tags" IS NOT NULL
            """,
            ct: ct);

        var tags = tagJsonRows
            .Select(ParseTags)
            .Where(t => t.Count > 0)
            .SelectMany(t => t)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .GroupBy(tag => tag.Trim())
            .Select(g => new { name = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(100)
            .Cast<object>()
            .ToList();

        return tags;
    }

    private static List<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }
}

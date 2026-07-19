using System.Text.Json;
using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.Services.Query;
using FluentAssertions;
using Moq;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// Tag aggregation helpers: JSON parse, trim, ignore blanks/invalid rows, count + top-100.
/// </summary>
public class TagServiceTests
{
    private static TagService Build(Mock<ICureFlowDbSession> db) => new(db.Object);

    private static void SetupTagRows(Mock<ICureFlowDbSession> db, params string?[] jsonRows)
    {
        db.Setup(x => x.QueryAsync<string>(
                It.Is<string>(s => s.Contains("Tags")),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonRows.Where(r => r is not null).Cast<string>().ToList());
    }

    private static string TagName(object row) =>
        (string)row.GetType().GetProperty("name")!.GetValue(row)!;

    private static int TagCount(object row) =>
        (int)row.GetType().GetProperty("count")!.GetValue(row)!;

    [Fact]
    public async Task ListAsync_aggregates_and_counts_trimmed_tags()
    {
        var db = new Mock<ICureFlowDbSession>();
        SetupTagRows(db,
            JsonSerializer.Serialize(new[] { " VIP ", "follow-up" }),
            JsonSerializer.Serialize(new[] { "VIP", "new" }));

        var tags = await Build(db).ListAsync();

        tags.Should().HaveCount(3);
        var vip = tags.Single(t => TagName(t) == "VIP");
        TagCount(vip).Should().Be(2);
        tags.Select(TagName).Should().Contain(["follow-up", "new"]);
    }

    [Fact]
    public async Task ListAsync_skips_blank_tags_and_invalid_json()
    {
        var db = new Mock<ICureFlowDbSession>();
        SetupTagRows(db,
            JsonSerializer.Serialize(new[] { "", "  ", "ok" }),
            "not-json",
            "null",
            JsonSerializer.Serialize(new[] { "ok" }));

        var tags = await Build(db).ListAsync();

        tags.Should().ContainSingle();
        TagName(tags[0]).Should().Be("ok");
        TagCount(tags[0]).Should().Be(2);
    }

    [Fact]
    public async Task ListAsync_returns_empty_when_no_tag_rows()
    {
        var db = new Mock<ICureFlowDbSession>();
        SetupTagRows(db);

        var tags = await Build(db).ListAsync();

        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_orders_by_count_descending_and_caps_at_100()
    {
        var db = new Mock<ICureFlowDbSession>();
        var many = Enumerable.Range(0, 120).Select(i => $"tag-{i:D3}").ToArray();
        // Duplicate first tag so it ranks highest.
        var rows = many.Select(t => JsonSerializer.Serialize(new[] { t }))
            .Append(JsonSerializer.Serialize(new[] { "tag-000" }))
            .ToArray();
        SetupTagRows(db, rows!);

        var tags = await Build(db).ListAsync();

        tags.Should().HaveCount(100);
        TagName(tags[0]).Should().Be("tag-000");
        TagCount(tags[0]).Should().Be(2);
    }
}

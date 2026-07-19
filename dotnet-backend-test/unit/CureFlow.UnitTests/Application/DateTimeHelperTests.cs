using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>UTC normalization used when Tasks (and others) persist DueAt / timestamptz.</summary>
public class DateTimeHelperTests
{
    [Fact]
    public void EnsureUtc_leaves_utc_unchanged()
    {
        var utc = new DateTime(2026, 7, 19, 10, 30, 0, DateTimeKind.Utc);
        var result = DateTimeHelper.EnsureUtc(utc);

        result.Should().Be(utc);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void EnsureUtc_treats_unspecified_as_utc()
    {
        var unspecified = new DateTime(2026, 7, 19, 10, 30, 0, DateTimeKind.Unspecified);
        var result = DateTimeHelper.EnsureUtc(unspecified);

        result.Should().Be(new DateTime(2026, 7, 19, 10, 30, 0, DateTimeKind.Utc));
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void EnsureUtc_converts_local_to_utc()
    {
        var local = new DateTime(2026, 7, 19, 10, 30, 0, DateTimeKind.Local);
        var result = DateTimeHelper.EnsureUtc(local);

        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(local.ToUniversalTime());
    }

    [Fact]
    public void EnsureUtc_nullable_passthroughs_null()
    {
        DateTimeHelper.EnsureUtc((DateTime?)null).Should().BeNull();
    }

    [Fact]
    public void EnsureUtc_nullable_normalizes_value()
    {
        DateTime? value = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        DateTimeHelper.EnsureUtc(value)!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}

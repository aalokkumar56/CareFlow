using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// Edge cases beyond <see cref="RoleNameRulesTests"/>: null/empty, multi-space,
/// underscore variants, and protected-name trimming.
/// </summary>
public class RoleNameRulesEdgeTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\tAdmin\t", "Admin")]
    public void Normalize_null_empty_and_whitespace(string? input, string expected) =>
        RoleNameRules.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData("A  B", "A__B")]
    [InlineData("Lead   Gen", "Lead___Gen")]
    [InlineData(" already_ok ", "already_ok")]
    [InlineData("Mixed Case Role", "Mixed_Case_Role")]
    public void Normalize_collapses_only_spaces_to_underscores(string input, string expected) =>
        RoleNameRules.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Admin", false)]
    [InlineData("Super_Admin", false)]
    [InlineData("SUPER_ADMIN", false)]
    [InlineData("super admin", false)]
    public void IsProtected_rejects_null_empty_and_near_matches(string? name, bool expected) =>
        RoleNameRules.IsProtected(name).Should().Be(expected);

    [Theory]
    [InlineData(" SuperAdmin ")]
    [InlineData("  superadmin  ")]
    [InlineData("SUPERADMIN")]
    public void IsProtected_trims_and_ignores_case_for_exact_SuperAdmin(string name) =>
        RoleNameRules.IsProtected(name).Should().BeTrue();

    [Fact]
    public void Normalize_then_IsProtected_round_trips_for_spaced_SuperAdmin()
    {
        // Spaces become underscores, so "Super Admin" is NOT protected.
        var normalized = RoleNameRules.Normalize("Super Admin");
        normalized.Should().Be("Super_Admin");
        RoleNameRules.IsProtected(normalized).Should().BeFalse();
        RoleNameRules.IsProtected("Super Admin").Should().BeFalse();
    }
}

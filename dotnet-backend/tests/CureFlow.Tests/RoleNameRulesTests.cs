using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.Tests;

public class RoleNameRulesTests
{
    [Theory]
    [InlineData("Billing Manager", "Billing_Manager")]
    [InlineData("  Front Desk  ", "Front_Desk")]
    [InlineData("SuperAdmin", "SuperAdmin")]
    public void Normalize_replaces_spaces_and_trims(string input, string expected) =>
        RoleNameRules.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("superadmin", true)]
    [InlineData("Admin", false)]
    [InlineData("Billing_Manager", false)]
    public void IsProtected_identifies_super_admin(string name, bool expected) =>
        RoleNameRules.IsProtected(name).Should().Be(expected);
}

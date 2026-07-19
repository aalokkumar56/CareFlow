using CureFlow.Infrastructure.External;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests;

public class WhatsappPhoneHelperTests
{
    [Theory]
    [InlineData("+91 98765 43210", "919876543210")]
    [InlineData("9876543210", "919876543210")]
    [InlineData("00919876543210", "919876543210")]
    [InlineData("919876543210", "919876543210")]
    [InlineData("7600174070", "917600174070")]
    [InlineData("+91 76001 74070", "917600174070")]
    public void Normalize_formats_indian_numbers(string input, string expected)
    {
        WhatsappPhoneHelper.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void Normalize_returns_empty_for_invalid_input(string input)
    {
        WhatsappPhoneHelper.Normalize(input).Should().BeEmpty();
    }

    [Fact]
    public void ToDisplay_formats_12_digit_indian_number()
    {
        WhatsappPhoneHelper.ToDisplay("9876543210").Should().Be("+91 98765 43210");
    }
}

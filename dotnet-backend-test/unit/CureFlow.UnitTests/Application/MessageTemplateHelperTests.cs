using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests.Application;

/// <summary>Template body rendering used by quick templates / appointment messages.</summary>
public class MessageTemplateHelperTests
{
    [Fact]
    public void Render_replaces_placeholders_case_insensitively()
    {
        var values = new Dictionary<string, string?>
        {
            ["name"] = "Riya",
            ["Doctor"] = "Dr. Mehta",
            ["date"] = "19 Jul 2026",
        };

        var result = MessageTemplateHelper.Render(
            "Namaste {NAME}, appt with {doctor} on {Date}.",
            values);

        result.Should().Be("Namaste Riya, appt with Dr. Mehta on 19 Jul 2026.");
    }

    [Fact]
    public void Render_null_value_becomes_empty_string()
    {
        var values = new Dictionary<string, string?> { ["name"] = null };

        MessageTemplateHelper.Render("Hi {name}!", values).Should().Be("Hi !");
    }

    [Fact]
    public void Render_leaves_unknown_placeholders_intact()
    {
        var values = new Dictionary<string, string?> { ["name"] = "Asha" };

        MessageTemplateHelper.Render("Hi {name}, see you at {time}.", values)
            .Should().Be("Hi Asha, see you at {time}.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Render_returns_null_or_empty_template_unchanged(string? template)
    {
        var values = new Dictionary<string, string?> { ["name"] = "X" };
        MessageTemplateHelper.Render(template!, values).Should().Be(template);
    }

    [Fact]
    public void AppointmentTemplateKeys_are_stable_snake_case_tokens()
    {
        AppointmentTemplateKeys.Confirmation.Should().Be("appointment_confirmation");
        AppointmentTemplateKeys.Rescheduled.Should().Be("appointment_rescheduled");
        AppointmentTemplateKeys.Cancelled.Should().Be("appointment_cancelled");
    }
}

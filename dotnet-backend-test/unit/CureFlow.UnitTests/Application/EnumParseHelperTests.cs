using CureFlow.Application.Common;
using CureFlow.Domain.Enums;
using FluentAssertions;
using Xunit;
using TaskStatusEnum = CureFlow.Domain.Enums.TaskStatus;

namespace CureFlow.UnitTests.Application;

/// <summary>Snake/Pascal enum parsing used by Tasks list status filter (and similar APIs).</summary>
public class EnumParseHelperTests
{
    [Theory]
    [InlineData("pending", TaskStatusEnum.Pending)]
    [InlineData("Pending", TaskStatusEnum.Pending)]
    [InlineData("in_progress", TaskStatusEnum.InProgress)]
    [InlineData("IN_PROGRESS", TaskStatusEnum.InProgress)]
    [InlineData("done", TaskStatusEnum.Done)]
    [InlineData("cancelled", TaskStatusEnum.Cancelled)]
    public void TryParseSnakeCase_parses_task_status(string input, TaskStatusEnum expected)
    {
        EnumParseHelper.TryParseSnakeCase<TaskStatusEnum>(input, out var result).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not_a_status")]
    [InlineData("in-progress")]
    public void TryParseSnakeCase_rejects_invalid_task_status(string? input)
    {
        EnumParseHelper.TryParseSnakeCase<TaskStatusEnum>(input, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("follow_up", TaskType.FollowUp)]
    [InlineData("post_visit_check_in", TaskType.PostVisitCheckIn)]
    [InlineData("Custom", TaskType.Custom)]
    public void TryParseSnakeCase_parses_task_type(string input, TaskType expected)
    {
        EnumParseHelper.TryParseSnakeCase<TaskType>(input, out var result).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("high", Priority.High)]
    [InlineData("Emergency", Priority.Emergency)]
    public void TryParseSnakeCase_parses_priority(string input, Priority expected)
    {
        EnumParseHelper.TryParseSnakeCase<Priority>(input, out var result).Should().BeTrue();
        result.Should().Be(expected);
    }
}

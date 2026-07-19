using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Services.CRM;
using FluentAssertions;
using Moq;
using Xunit;
using TaskStatusEnum = CureFlow.Domain.Enums.TaskStatus;

namespace CureFlow.UnitTests;

/// <summary>Task create/list/update validation: UTC DueAt, status filter, not-found.</summary>
public class TaskServiceValidationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PatientId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static (TaskService Svc, Mock<ICureFlowDbSession> Db, Mock<INotificationPublisher> Notify)
        Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(TenantId);
        db.SetupGet(x => x.UserId).Returns(UserId);

        var notify = new Mock<INotificationPublisher>();
        notify.Setup(x => x.PublishAsync(It.IsAny<NotificationPublishRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (new TaskService(db.Object, notify.Object), db, notify);
    }

    [Fact]
    public async Task CreateAsync_normalizes_unspecified_DueAt_to_utc()
    {
        var (svc, db, _) = Build();
        TaskItem? captured = null;
        db.Setup(x => x.InsertAsync(It.IsAny<TaskItem>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, bool, CancellationToken>((t, _, _) => captured = t)
            .Returns(Task.CompletedTask);

        var due = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Unspecified);
        await svc.CreateAsync("Call back", TaskType.Callback, null, null, due, Priority.Medium);

        captured.Should().NotBeNull();
        captured!.DueAt.Should().NotBeNull();
        captured.DueAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        captured.DueAt.Should().Be(DateTimeHelper.EnsureUtc(due));
    }

    [Fact]
    public async Task CreateAsync_resolves_patient_name_when_patient_exists()
    {
        var (svc, db, _) = Build();
        db.Setup(x => x.GetByIdAsync<Patient>(PatientId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = PatientId, Name = "Neha", TenantId = TenantId });
        TaskItem? captured = null;
        db.Setup(x => x.InsertAsync(It.IsAny<TaskItem>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, bool, CancellationToken>((t, _, _) => captured = t)
            .Returns(Task.CompletedTask);

        await svc.CreateAsync("Follow up", TaskType.FollowUp, PatientId, null, null, Priority.Low);

        captured!.PatientName.Should().Be("Neha");
        captured.PatientId.Should().Be(PatientId);
    }

    [Fact]
    public async Task CreateAsync_publishes_notification_when_assigned()
    {
        var (svc, db, notify) = Build();
        var assignee = Guid.NewGuid();
        db.Setup(x => x.InsertAsync(It.IsAny<TaskItem>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await svc.CreateAsync(
            "Assigned task", TaskType.Custom, null, null, null, Priority.High, assignee);

        notify.Verify(x => x.PublishAsync(
            It.Is<NotificationPublishRequest>(r =>
                r.Type == NotificationTypeCodes.TaskAssigned
                && r.TargetUserIds != null
                && r.TargetUserIds.Contains(assignee)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_skips_notification_when_unassigned()
    {
        var (svc, db, notify) = Build();
        db.Setup(x => x.InsertAsync(It.IsAny<TaskItem>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await svc.CreateAsync("Solo", TaskType.Custom, null, null, null, Priority.Low);

        notify.Verify(x => x.PublishAsync(
            It.IsAny<NotificationPublishRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListAsync_applies_status_filter_for_snake_case()
    {
        var (svc, db, _) = Build();
        string? sql = null;
        object? param = null;
        db.Setup(x => x.QueryAsync<TaskItem>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, p, _, _) =>
            {
                sql = s;
                param = p;
            })
            .ReturnsAsync(Array.Empty<TaskItem>());

        await svc.ListAsync("in_progress");

        sql.Should().Contain(@"""Status"" = @status");
        param.Should().NotBeNull();
        param!.GetType().GetProperty("status")!.GetValue(param)
            .Should().Be((int)TaskStatusEnum.InProgress);
    }

    [Fact]
    public async Task ListAsync_ignores_invalid_status_filter()
    {
        var (svc, db, _) = Build();
        string? sql = null;
        db.Setup(x => x.QueryAsync<TaskItem>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, bool, CancellationToken>((s, _, _, _) => sql = s)
            .ReturnsAsync(Array.Empty<TaskItem>());

        await svc.ListAsync("not_a_real_status");

        sql.Should().NotContain(@"""Status"" = @status");
    }

    [Fact]
    public async Task UpdateAsync_throws_when_task_missing()
    {
        var (svc, db, _) = Build();
        db.Setup(x => x.GetByIdAsync<TaskItem>(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        var act = () => svc.UpdateAsync(Guid.NewGuid(), TaskStatusEnum.Done);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Task*");
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_existing_task()
    {
        var (svc, db, _) = Build();
        var task = new TaskItem { Id = Guid.NewGuid(), Title = "T", TenantId = TenantId };
        db.Setup(x => x.GetByIdAsync<TaskItem>(task.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        TaskItem? updated = null;
        db.Setup(x => x.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, bool, CancellationToken>((t, _, _) => updated = t)
            .Returns(Task.CompletedTask);

        await svc.DeleteAsync(task.Id);

        updated.Should().NotBeNull();
        updated!.IsDeleted.Should().BeTrue();
    }
}

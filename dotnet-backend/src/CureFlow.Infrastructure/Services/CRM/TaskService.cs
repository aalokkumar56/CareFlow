using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.CRM;

public class TaskService : ITaskService
{
    private readonly ICureFlowDbSession _db;
    private readonly INotificationPublisher _notifications;

    public TaskService(ICureFlowDbSession db, INotificationPublisher notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<Guid> CreateAsync(string title, TaskType type, Guid? patientId,
        string? notes, DateTime? dueAt, Priority priority, Guid? assignedTo = null, CancellationToken ct = default)
    {
        string? patientName = null;
        if (patientId.HasValue)
        {
            var p = await _db.GetByIdAsync<Patient>(patientId.Value, ct: ct);
            patientName = p?.Name;
        }

        var t = new TaskItem
        {
            Title = title,
            Type = type,
            PatientId = patientId,
            PatientName = patientName,
            Notes = notes,
            DueAt = DateTimeHelper.EnsureUtc(dueAt),
            Priority = priority,
            AssignedTo = assignedTo,
        };
        await _db.InsertAsync(t, ct: ct);

        if (assignedTo.HasValue)
        {
            await _notifications.PublishAsync(new NotificationPublishRequest(
                NotificationTypeCodes.TaskAssigned,
                "Task assigned to you",
                title,
                NotificationSeverity.Info,
                EntityType: "task",
                EntityId: t.Id,
                ActionUrl: $"/tasks?id={t.Id}",
                TargetUserIds: [assignedTo.Value],
                AssignedStaffId: assignedTo,
                CreatorUserId: _db.UserId), ct);
        }

        return t.Id;
    }

    public async Task<IReadOnlyList<object>> ListAsync(string? status, CancellationToken ct = default)
    {
        var where = SqlFragments.WhereActive<TaskItem>(ignoreTenant: false);
        var sql = $"""
            SELECT * FROM "Tasks"
            WHERE {where}
            """;
        object? param = null;
        if (!string.IsNullOrEmpty(status)
            && EnumParseHelper.TryParseSnakeCase<Domain.Enums.TaskStatus>(status, out var s))
        {
            sql += """ AND "Status" = @status""";
            param = new { status = (int)s };
        }

        sql += """
             ORDER BY "CreatedAt" DESC
             LIMIT 500
            """;

        var rows = await _db.QueryAsync<TaskItem>(sql, param, ct: ct);
        return rows.Cast<object>().ToList();
    }

    public async Task UpdateAsync(Guid id, Domain.Enums.TaskStatus? status, CancellationToken ct = default)
    {
        var t = await _db.GetByIdAsync<TaskItem>(id, ct: ct) ?? throw new NotFoundException("Task");
        if (status.HasValue) t.Status = status.Value;
        await _db.UpdateAsync(t, ct: ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.GetByIdAsync<TaskItem>(id, ct: ct) ?? throw new NotFoundException("Task");
        t.IsDeleted = true;
        await _db.UpdateAsync(t, ct: ct);
    }
}

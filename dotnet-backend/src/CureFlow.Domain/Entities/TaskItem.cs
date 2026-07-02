using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace CureFlow.Domain.Entities;

public class TaskItem : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public TaskType Type { get; set; } = TaskType.FollowUp;
    public Guid? PatientId { get; set; }
    public string? PatientName { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? AssignedTo { get; set; }
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Pending;
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTime? DueAt { get; set; }
    public string? Notes { get; set; }
}

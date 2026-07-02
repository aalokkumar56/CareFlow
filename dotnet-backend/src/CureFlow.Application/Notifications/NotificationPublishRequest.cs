using CureFlow.Domain.Enums;

namespace CureFlow.Application.Notifications;

public sealed record NotificationPublishRequest(
    string Type,
    string Title,
    string? Body = null,
    NotificationSeverity Severity = NotificationSeverity.Info,
    string? EntityType = null,
    Guid? EntityId = null,
    string? ActionUrl = null,
    string? DedupeKey = null,
    IReadOnlyList<Guid>? TargetUserIds = null,
    Guid? DoctorUserId = null,
    Guid? AssignedStaffId = null,
    Guid? CreatorUserId = null);

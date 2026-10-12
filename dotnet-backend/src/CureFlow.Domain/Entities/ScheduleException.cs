using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;

public sealed class ScheduleException : TenantEntity
{
    public Guid FacilityId { get; set; }
    public Guid? PractitionerUserId { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid OperatingSettingId { get; set; }
    public string Timezone { get; set; } = "";
    public string Kind { get; set; } = "block";
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public long Revision { get; set; } = 1;
    public DateTime? DisabledAt { get; set; }
}

/// <summary>Retained potential impact on a booking; never cancels or moves that booking.</summary>
public sealed class AppointmentRescheduleWork : TenantEntity
{
    public Guid ExceptionId { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public DateTime OriginalScheduledAtUtc { get; set; }
    public int OriginalDurationMinutes { get; set; }
    public bool FacilityAssociationNeedsReview { get; set; } = true;
    public string State { get; set; } = "needs_review";
}

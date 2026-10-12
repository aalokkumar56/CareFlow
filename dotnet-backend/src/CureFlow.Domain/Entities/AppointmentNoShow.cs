using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class AppointmentNoShow : TenantEntity
{
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public Guid FacilityId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid FollowUpWorkId { get; set; }
    public long OriginalAppointmentRevision { get; set; }
    public long AppointmentRevision { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int CutoffMinutesAfterStart { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public string PolicyVersion { get; set; } = "";
}

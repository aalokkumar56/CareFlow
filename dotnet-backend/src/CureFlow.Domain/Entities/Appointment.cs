using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

public class Appointment : TenantEntity
{
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public Guid? DoctorUserId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 15;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public string? ChiefComplaint { get; set; }
    public string? Notes { get; set; }
    public bool ReminderSent { get; set; }
    public bool FollowUpSent { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal? ConsultationFee { get; set; }
    public bool IsPaid { get; set; }
}

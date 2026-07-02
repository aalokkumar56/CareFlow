using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

/// <summary>Clinical visit / encounter — central anchor for EHR records during a single visit.</summary>
public class Visit : TenantEntity
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid DoctorUserId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? Department { get; set; }

    public DateTime VisitDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.InProgress;
    public string VisitType { get; set; } = "outpatient";

    public string? ChiefComplaint { get; set; }
    public string? Symptoms { get; set; }
    public string? Diagnosis { get; set; }
    public string? DoctorNotes { get; set; }
    public string? FollowUpAdvice { get; set; }
    public DateTime? FollowUpDate { get; set; }

    /// <summary>JSON array placeholder for attachment metadata.</summary>
    public string AttachmentsJson { get; set; } = "[]";
}

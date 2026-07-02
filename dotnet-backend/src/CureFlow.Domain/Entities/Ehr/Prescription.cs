using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>A prescription issued by a doctor at a particular visit/encounter.</summary>
public class Prescription : TenantEntity
{
    public Guid PatientId { get; set; }
    public Guid DoctorUserId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid? AppointmentId { get; set; }
    public Guid? VisitId { get; set; }
    public DateTime PrescribedAt { get; set; } = DateTime.UtcNow;

    public string? Diagnosis { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? FollowUpAdvice { get; set; }
    public DateTime? NextVisitDate { get; set; }

    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    public ICollection<Injection> Injections { get; set; } = new List<Injection>();
}



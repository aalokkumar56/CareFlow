using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>Clinical note made by a doctor during/after a visit.</summary>
public class ClinicalNote : TenantEntity
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string NoteType { get; set; } = "progress";
    public string Subjective { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Assessment { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
}

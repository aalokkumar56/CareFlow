using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>Lab report - link to an uploaded file or external lab.</summary>
public class LabReport : TenantEntity
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? OrderedByUserId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? LabName { get; set; }
    public DateTime? SampleCollectedAt { get; set; }
    public DateTime? ReportedAt { get; set; }
    public string? Results { get; set; }
    public string? Interpretation { get; set; }
    public string? FileUrl { get; set; }
    public bool IsAbnormal { get; set; }
}
